using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Services
{
    public class BankService : IBankService
    {
        private readonly ApplicationDbContext _context;

        public BankService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task RegisterIncomeFromPaymentAsync(Payment payment)
        {
            if (!payment.BankID.HasValue)
            {
                return; // No es un pago bancario o no se especificó el banco.
            }

            // Verificar si ya existe una transacción activa (ej. desde SalesController)
            if (_context.Database.CurrentTransaction != null)
            {
                await ProcessIncomeInternalAsync(payment);
            }
            else
            {
                // Si no hay transacción, creamos una nueva con bloqueo Serializable
                using (var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        await ProcessIncomeInternalAsync(payment);
                        await dbTransaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await dbTransaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        private async Task ProcessIncomeInternalAsync(Payment payment)
        {
            var bankToUpdate = await _context.Banks.FindAsync(payment.BankID.Value);
            if (bankToUpdate == null) throw new InvalidOperationException($"Banco con ID {payment.BankID.Value} no encontrado.");

            bankToUpdate.Balance += payment.Amount;

            var bankTransaction = new BankTransaction
            {
                BankID = bankToUpdate.BankID,
                Date = DateTime.Now,
                Type = BankTransactionType.SaleIncome,
                Amount = payment.Amount,
                Description = $"Ingreso por venta #{payment.InvoiceID}",
                BalanceAfterTransaction = bankToUpdate.Balance,
                PaymentID = payment.PaymentID
            };

            _context.BankTransactions.Add(bankTransaction);
            await _context.SaveChangesAsync();
        }

        // Implementación de transferencias entre bancos
        public async Task RegisterTransferAsync(int sourceBankId, int targetBankId, decimal amount, string description)
        {
            // Misma lógica para transferencias: reutilizar transacción si existe
            if (_context.Database.CurrentTransaction != null)
            {
                await RegisterManualMovementAsync(sourceBankId, BankTransactionType.TransferOut, amount, $"{description} (A Banco ID: {targetBankId})");
                await RegisterManualMovementAsync(targetBankId, BankTransactionType.TransferIn, amount, $"{description} (De Banco ID: {sourceBankId})");
            }
            else
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        await RegisterManualMovementAsync(sourceBankId, BankTransactionType.TransferOut, amount, $"{description} (A Banco ID: {targetBankId})");
                        await RegisterManualMovementAsync(targetBankId, BankTransactionType.TransferIn, amount, $"{description} (De Banco ID: {sourceBankId})");
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task RegisterManualMovementAsync(int bankId, BankTransactionType type, decimal amount, string description)
        {
            var bank = await _context.Banks.FindAsync(bankId);
            if (bank == null) throw new InvalidOperationException($"Banco con ID {bankId} no encontrado.");

            // Determinar si el movimiento suma o resta al saldo
            bool isIncome = type == BankTransactionType.SaleIncome || 
                            type == BankTransactionType.TransferIn || 
                            type == BankTransactionType.ManualIncome;

            // Validar que el saldo sea suficiente para movimientos de egreso
            if (!isIncome && bank.Balance < amount)
            {
                throw new InvalidOperationException($"Saldo insuficiente en el banco '{bank.Name}'. Saldo actual: {bank.Balance:C}, Monto a retirar: {amount:C}.");
            }

            bank.Balance += isIncome ? amount : -amount;

            var transaction = new BankTransaction
            {
                BankID = bankId,
                Date = DateTime.Now,
                Type = type,
                Amount = amount,
                Description = description,
                BalanceAfterTransaction = bank.Balance
            };

            _context.BankTransactions.Add(transaction);
            await _context.SaveChangesAsync();
        }
    }
}