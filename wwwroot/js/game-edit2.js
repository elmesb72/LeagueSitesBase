import { Scorecard } from "./s/scorecard.js";

document.addEventListener("DOMContentLoaded", () => {
    const scorecardContainer = document.querySelector(".game-edit-scorecard");
    let scorecard = new Scorecard(scorecardContainer);
    scorecard.getScorecards().then(() => {
        scorecard.container.replaceChildren();
        scorecard.render();
    });
});