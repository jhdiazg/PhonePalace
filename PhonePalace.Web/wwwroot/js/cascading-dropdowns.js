$(document).ready(function () {
    // Function to load municipalities
    function loadMunicipalities(departmentId, selectedMunicipalityId) {
        var $municipalitySelect = $('#MunicipalityID');
        $municipalitySelect.empty(); // Clear existing options
        $municipalitySelect.append($('<option></option>').val("").text("Cargando municipios..."));

        if (departmentId) {
            // Determine the controller from the current URL to construct the endpoint
            // Assumes URL structure like /Controller/Action/Id
            var pathSegments = window.location.pathname.split('/');
            var controller = pathSegments[1] || "Home";
            var url = '/' + controller + '/GetMunicipalities';

            $.ajax({
                url: url,
                type: "GET",
                dataType: "JSON",
                data: { departmentId: departmentId },
                success: function (municipalities) {
                    $municipalitySelect.empty();
                    $municipalitySelect.append($('<option></option>').val("").text("Seleccione un municipio"));
                    $.each(municipalities, function (i, municipality) {
                        $municipalitySelect.append($('<option></option>').val(municipality.value).text(municipality.text));
                    });
                    if (selectedMunicipalityId) {
                        $municipalitySelect.val(selectedMunicipalityId);
                    }
                },
                error: function () {
                    $municipalitySelect.empty();
                    $municipalitySelect.append($('<option></option>').val("").text("Error al cargar municipios"));
                }
            });
        } else {
            $municipalitySelect.empty();
            $municipalitySelect.append($('<option></option>').val("").text("Seleccione un departamento"));
        }
    }

    // Event listener for DepartmentID change
    $('#DepartmentID').change(function () {
        var departmentId = $(this).val();
        loadMunicipalities(departmentId);
    });
});