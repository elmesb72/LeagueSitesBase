const container = document.getElementsByClassName('scores-date-picker-month')[0];
container.addEventListener("click", (event) => {
    const datepickers = document.getElementsByClassName('datepicker');
    if (datepickers.length === 0) { // create it
        const container = document.getElementsByClassName('scores-date-picker-month')[0];
        new Datepicker(container, {
            todayButton: true,
        });
        container.addEventListener("changeDate", (event) => {
            const datepicker = document.getElementsByClassName('scores-date-picker-month')[0].datepicker;
            const focusedDate = datepicker.getFocusedDate("yyyy-mm-dd");
            window.location.href = `/Scores/${focusedDate}`;
        });
    }
    else { // destroy it
        const container = document.getElementsByClassName('scores-date-picker-month')[0];
        if (event.target === container || event.target instanceof SVGElement || event.target.classList.contains("scores-date-picker-month-name")) {
            const datepicker = container.datepicker;
            const date = datepicker.getFocusedDate("mm-dd-yyyy");
            datepicker.destroy();
            container.setAttribute("data-date", date);
        }
    }
});