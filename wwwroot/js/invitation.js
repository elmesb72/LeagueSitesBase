$('#Emails').selectize({
    plugins: ['remove_button'],
    delimiter: ',',
    persist: false,
    create: function(input) {
        return {
            value: input,
            text: input
        }
    },
    selectOnTab: true,
    createOnBlur: true
});

document.addEventListener('DOMContentLoaded', function() {
    // Highlight current number if existing invitation
    if ($('#PlayerNumber').val() != '') {
        $('#number-selector-' + $('#PlayerNumber').val()).addClass('number-selected');
    }

    var team = $('#TeamID').val();
    getTeamPlayersDictionary(team);
    getUserPermissionsDictionary(team)
}, false);

function updateNumber(number) {
    if ($('#PlayerNumber').val() != '') {
        $('#number-selector-' + $('#PlayerNumber').val()).removeClass('number-selected');
    }
    if ($('#PlayerNumber').val() != number) {
        $('#PlayerNumber').val(number);
        $('#number-selector-' + $('#PlayerNumber').val()).addClass('number-selected');
    }
    else {
        $('#PlayerNumber').val('');
    }
}

function updateTeam(id) {
    // Update team number and styling
    if (id == $('#TeamID').val()) {
        $('#team-selector-' + id).removeClass("team-selected");
        $('#TeamID').val('-1');
    }
    else {
        $('#team-selector-' + $('#TeamID').val()).removeClass("team-selected");
        $('#team-selector-' + id).addClass("team-selected");
        $('#TeamID').val(id);
    }

    // Clear player number and refresh team available numbers
    if (id != '-1') {
        $('div[class="number-selected"]').removeClass("number-selected");
        $('#PlayerNumber').val('');
    }
    var team = $('#TeamID').val();
    getTeamPlayersDictionary(team);
    getUserPermissionsDictionary(team)
}

function getTeamPlayersDictionary(teamID) {
    var url = "/api/TeamPlayers/" + teamID;
    if ($('#PlayerNumber').val() != '') {
        url = url + "?exclude=" + $('#PlayerNumber').val();
    }
    $.get(url, function (response) { updateNumberSelector(response) }, "json");
}

function updateNumberSelector(dictionary) {
    // Reset so all are clickable and not taken
    $("div[id^='number-selector-']").each(function() {
        var thisNum = $(this).attr("id").split('-').pop();
        $(this).attr("onclick", "updateNumber('" + thisNum + "')").bind("click");
        $(this).removeClass('number-taken');
    });
    // Remove click function from taken numbers excluding this player and set tooltips
    $.each(dictionary, function(key, value) {
        $('#number-selector-' + key).addClass('number-taken');
        $('#number-selector-' + key).attr("onclick", "").unbind("click");
        $('#number-selector-' + key).attr("title", value);
    });
}

function getUserPermissionsDictionary(teamID) {
    var url = "/api/User/Permissions/" + teamID;
    $.get(url, function (response) { updatePermissionsSelector(response) }, "json");
}

function updatePermissionsSelector(dictionary) {
    var isNewInvitation = true;
    if ($('#Emails').prop('disabled')) {
        isNewInvitation = false;
    }

    if (dictionary["Webmaster"] && !isNewInvitation) {
        $('#Webmaster').prop('disabled', false);
    }
    else {
        $('#Webmaster').prop('disabled', true);
    }

    if ((dictionary["Webmaster"] || dictionary["Executive"]) && !isNewInvitation) {
        $('#Executive').prop('disabled', false);
    }
    else {
        $('#Executive').prop('disabled', true);
    }

    if ((dictionary["Webmaster"] || dictionary["Executive"] || dictionary["Manager"]) 
            && $('#TeamID').val() != "-1") {
        $('#Manager').prop('disabled', false);
    }
    else {
        $('#Manager').prop('disabled', true);
    }

    if ((dictionary["Webmaster"] || dictionary["Executive"] || dictionary["Manager"] || dictionary["Scorer"]) 
            && $('#TeamID').val() != "-1") {
        $('#Scorer').prop('disabled', false);
    }
    else {
        $('#Scorer').prop('disabled', true);
    }
}


// Validation

$(document).ready(function(){
    $('#form').submit(function(event) {
        var errors = [];
        var playerExists = $('#PlayerExists').val();
        if (playerExists == "True") {
            if ($('#FirstName').val() == "") {
                errors.push('First Name must be set!');
            }
            if ($('#LastName').val() == "") {
                errors.push('Last Name must be set!');
            }
        }

        var userExists = $('#UserExists').val();
        if (userExists == "False") {
            if ($('#Emails').val().length == 0) {
                errors.push('At least one email must be set!');
            }
        }

        if (errors.length > 0) {
            event.preventDefault();
            $('#error-text').html(errors.join('<br />'));
        }
    });
}, false);