function updateBG(jscolor) {
    $('.team-header').css("background-color", '#' + jscolor);
}
function updateColor(jscolor) {
    $('.team-header').css("color", '#' + jscolor);
}

// Validation

$(document).ready(function(){
    $('#team-edit-form').submit(function(event) {
        var errors = [];
        if ($('#Location').val().length == 0) {
            errors.push('Location name is required.');
        }
        if ($('#Name').val().length == 0) {
            errors.push('Team name is required.');
        }
        var abbrLen = $('#Abbr').val().length;
        if (abbrLen < 2 || abbrLen > 3) {
            errors.push('Abbreviation must be either 2 or 3 characters.');
        }
        var bgregex = /[A-Fa-f0-9]{6}/g;
        if (bgregex.test($('#BackgroundColor').val())) {}
        else {
            errors.push('Background Colour must be a 6-character hexadecimal value.')
        }
        var textregex = /[A-Fa-f0-9]{6}/g;
        if (textregex.test($('#TextColor').val())) {}
        else {
            errors.push('Text Colour must be a 6-character hexadecimal value.')
        }

        if (errors.length > 0) {
            event.preventDefault();
            $('#error-text').html(errors.join('<br />'));
        }
    });
}, false);