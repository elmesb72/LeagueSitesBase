function updateGameStatus(gameStatus)
{
    switch ($("label[for='" + gameStatus.id + "']").text()) {
        case 'Upcoming':
            $('#score-visitor').prop('disabled', false);
            $('#score-visitor').val($('#score-visitor-orig')[0].value);
            $('#score-visitor').prop('placeholder', 0);
            $('#score-host').prop('disabled', false);
            $('#score-host').val($('#score-host-orig')[0].value);
            $('#score-host').prop('placeholder', 0);
            break;
        case 'Played':
            console.log('Played!');
            $('#score-visitor').prop('disabled', false);
            $('#score-visitor').val($('#score-visitor-orig')[0].value);
            $('#score-visitor').prop('placeholder', 0);
            $('#score-host').prop('disabled', false);
            $('#score-host').val($('#score-host-orig')[0].value);
            $('#score-host').prop('placeholder', 0);            
            break;
        default:
            $('#score-visitor').prop('disabled', true);
            $('#score-visitor').val('');
            $('#score-visitor').prop('placeholder', '');
            $('#score-host').prop('disabled', true);
            $('#score-host').val('');
            $('#score-host').prop('placeholder', '');
            break;


    }
}

function updateScore(score, team) {
    $('#score-' + team + '-orig').val(score.value);
    $('#game-status-id-Played').prop('checked', true);
}