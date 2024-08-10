function forget(userLoginID)
{
    $.ajax({
        type : "DELETE",
        url : "/api/UserLogin/Delete/" + userLoginID,
        contentType: "application/json; charset=UTF-8",
        success: function (response) {  
            location.reload();
        },
        error: function (xhr, ajaxOptions, thrownError) {
            alert(xhr.responseText);
        }
    });
}

function favourite(userLoginID)
{
    $.ajax({
        type : "POST",
        url : "/api/UserLogin/Favourite/" + userLoginID,
        contentType: "application/json; charset=UTF-8",
        success: function (response) {  
            location.reload();
        },
        error: function (xhr, ajaxOptions, thrownError) {
            alert(xhr.responseText);
        }
    });
}