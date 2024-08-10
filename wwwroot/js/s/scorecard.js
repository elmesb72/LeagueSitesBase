import { Baseball } from './baseball.js';

export class Scorecard {

    container;
    type;
    host;
    scorecards;

    constructor(container) {
        this.container = container;
        this.host = true;
        this.type = "standard";
    }

    async getScorecards() {
        const game = this.container.id;
        const response = await fetch(`/api/Scorecard/${game}`);
        this.scorecards = await response.json();
        //console.log(this.scorecards);
    }

    render() {
        this.container.replaceChildren();
        this.container.textContent = "Rendering Scorecard...";

        switch (this.type) {
            case "standard":
                //console.log("Rendering standard scorecard...");
                const standardScorecard = this.renderStandard();
                if (standardScorecard) {
                    this.container.textContent = null;
                    this.container.appendChild(standardScorecard);
                    //this.appendChild(standardScorecard);
                }
                else {
                    this.container.textContent = "Error loading standard scorecard";
                }
                break;
            default:
                this.container.textContent = "Unable to render scorecard; type unknown or not set";
        }

        // Apply external styles to the shadow DOM
        const cssLink = document.createElement("link");
        cssLink.setAttribute("rel", "stylesheet");
        cssLink.setAttribute("href", "/css/scorecard.css");

        // Attach the created element to the shadow DOM
        this.container.appendChild(cssLink);
    }

    renderStandard() {
        const scorecardTable = document.createElement('table');
        scorecardTable.className = "scorecard-table";

        const page = this.host ? this.scorecards.hostSide : this.scorecards.visitorSide;

        // Determine the box starting each inning
        const inningStartMap = this.determineInningStart(page);
        const lastBox = Math.max(...page.events.map(event => event.index)); // Used for later calculations
        // inningStartMap always assumes the next inning, but if that value is greater than the number of events recorded, then the last "inning" should not exist.
        /* A better way of doing this might be to have this map determine both a first and last box (aka a range) for each inning. 
            This would then also cover the edge case where a baserunner is the third out and the same batter goes up the following inning.
        */
        const [lastKey, lastValue] = [...inningStartMap].at(-1) || [];
        if (lastValue > lastBox) {
            inningStartMap.delete(lastKey);
        }

        const rows = Math.max(...page.lineup.map(row => row.row));

        // Draw table header row
        const inningRow = document.createElement('tr');
        inningRow.className = 'game-edit-header-row';

        // Draw player info header cell
        const playerHeaderCell = document.createElement('th');
        playerHeaderCell.textContent = "Player";
        inningRow.appendChild(playerHeaderCell);

        // Draw inning number cells. Repeat an inning number if the team bats around.
        let columns = 1;
        inningStartMap.forEach((firstBox, inning) => {
            // How many times did this inning wrap?
            // How long was the inning? (How many batters appeared?)
            let numBoxes = 3; // The minimum; a reasonable default number.
            // Use next inning's first box, 
            if (inningStartMap.has(inning + 1)) {
                numBoxes = inningStartMap.get(inning + 1) - firstBox;
            }
            else { // or if it's the last inning, use the last box.
                numBoxes = lastBox - firstBox + 1;
            }
            const boxesPerRow = numBoxes/rows;
            const timesBattedAround = Math.floor(boxesPerRow) - (numBoxes % rows === 0 ? 1 : 0); 
            // If there's no modulus/remainder, it means the same number of batter and rows, which isn't batting around

            for (let i = 0; i <= timesBattedAround; i++) {
                columns++;
                const inningNumber = document.createElement('th');
                inningNumber.textContent = `${inning}`;
                inningRow.appendChild(inningNumber);
            }
        });

        scorecardTable.appendChild(inningRow);

        /* Determine row and column indeces for each event box, using the same bat-around logic for the header.
            Assumes input (events list) is sorted by index (aka box number).
            Map format is (location, event.index), where location is a tuple (column, row).
            Missing key would mean an empty box. */
        /* At the same time, track advancement of each batter during consequent events. The standard scoresheet
            boxes reflect end results - how far the batter progressed - where a project scoresheet entry (as the 
            data is represented) only shows what happens within each player's plate appearance.
            Results are to be attached to the event.
        */
        const eventPositionMap = new Map();
        let row = 1, column = 1, inning = 1, outs = 0;
        const baseStatus = [null, null, null, null]; // If unoccupied, null, else event index. Array 0..3 refers to batter, first, second, third, respectively.

        const lineScoreMap = new Map();
        lineScoreMap.set(inning, { runs: 0, hits: 0 }); // Add first inning to line score
        page.events.forEach(event => {
            // If this event starts the next inning, increment the inning and the column.
            if (inningStartMap.has(inning + 1) && inningStartMap.get(inning + 1) === event.index) {
                inning++;
                lineScoreMap.set(inning, { runs: 0, hits: 0 }); // Add inning to line score
                column++;
            }
            // If the map already has an event here, the team has batted around, so increment the column.
            if (eventPositionMap.has(`${column}-${row}`)) {
                column++;
            }
            // Assign the event to the current column and row.
            eventPositionMap.set(`${column}-${row}`, event.index);
            // Move down a row.
            row++;
            if (row > rows) {
                row = 1;
            }

            // Set the batter
            baseStatus[0] = event.index;
            event.rbis = 0;
            //console.log(`Now batting: ${event.index}`);

            // Did anyone move before the event result?
            if (event.before) {
                // Plays causing movements are separated by semi-colons.
                event.before.split(';').forEach(movementMoment => {
                    // For the sake of tracking advancement, we don't care what or how, which is noted after a '/' slash, so we strip that.
                    const playerMovementMoment = movementMoment.split('/')[0];
                    // Multiple movements are separated by commas.
                    playerMovementMoment.split(',').forEach(playerMovement => {
                        let movementResults = Scorecard.movePlayers(page.events, event, playerMovement, baseStatus, outs);
                        outs += movementResults.outs;
                        lineScoreMap.get(inning).runs += movementResults.runs;
                    });
                });
            }

            // Did the batter reach base?
            const batterReached = Baseball.codesNotOut.find(code => event.during.startsWith(code));
            let movedFirstAfter = false;
            if (batterReached) {
                // If so, then check if the base is occupied.
                let baseReached = null;
                switch (batterReached) {
                    case "S":
                        lineScoreMap.get(inning).hits++;
                    case "W":
                    case "IW":
                    case "HBP":
                    case "INTF":
                    case "E":
                    case "FO":
                    case "FC":
                        baseReached = 1; break;
                    case "D":
                        lineScoreMap.get(inning).hits++;
                        baseReached = 2; break;
                    case "T":
                        lineScoreMap.get(inning).hits++;
                        baseReached = 3; break;
                    case "HR":
                        lineScoreMap.get(inning).hits++;
                        lineScoreMap.get(inning).runs++;
                        event.rbis++;
                        event.advancement = "H"; break;
                    default:
                        console.error(`Could not determine which base the batter reached from code "${batterReached}`);
                }

                if ((baseReached === 1 && baseStatus[1]) || (baseReached === 2 && baseStatus[2]) || (this.baseReached === 3 && baseStatus[3])) {
                    //console.log(`Batter reached occupied base ${baseReached} (${batterReached}); moving baserunners first...`);
                    // Move those already on base first (the first movement moment in "event.after").
                    movedFirstAfter = true;
                    // We don't care what or how, which is noted after a '/' slash, so we strip that.
                    const firstMovement = event.after.split(";")[0];
                    const firstMovementWithoutReason = firstMovement.split('/')[0];
                    const firstMovementMoments = firstMovementWithoutReason.split(',');
                    firstMovementMoments.forEach(playerMovementMoment => {
                        // Multiple movements are separated by commas.
                        playerMovementMoment.split(',').forEach(playerMovement => {
                            let movementResults = Scorecard.movePlayers(page.events, event, playerMovement, baseStatus, outs);
                            outs += movementResults.outs;
                            lineScoreMap.get(inning).runs += movementResults.runs;
                        });
                    });
                }
                else {
                    //console.log(`Batter reached unoccupied base (${batterReached})`);
                }

                // Then assign the base - it should be free now
                switch (baseReached) {
                    case 1:
                        baseStatus[1] = event.index;
                        event.advancement = outs === 3 ? null : '1'; // Unless third out (reached on force out or fielder's choice)
                        break;
                    case 2:
                        baseStatus[2] = event.index;
                        event.advancement = '2';
                        break;
                    case 3:
                        baseStatus[3] = event.index;
                        event.advancement = '3';
                        break;
                }
            }
            else if (!event.after || !event.after.includes('B-')) {
                //console.log(`Batter did not reach (${event.during})`);
                outs++;
                event.out = outs;
            }
            else {
                //console.log(`Batter reached on error (${event.during})`);
            }

            if (event.after) {
                event.rbis += ((event.after || '').match(/-H/g) || []).length;
                // Plays causing movements are separated by semi-colons.
                let afterEvents = event.after.split(";");
                if (movedFirstAfter) {
                    afterEvents = afterEvents.slice(1); // Already processed first "after", so exclude it
                }

                afterEvents.forEach(movementMoment => {
                    // For the sake of tracking advancement, we don't care what or how, which is noted after a '/' slash, so we strip that.
                    const playerMovementMoment = movementMoment.split('/')[0];
                    // Multiple movements are separated by commas.
                    playerMovementMoment.split(',').forEach(playerMovement => {
                        let movementResults = Scorecard.movePlayers(page.events, event, playerMovement, baseStatus, outs);
                        outs += movementResults.outs;
                        lineScoreMap.get(inning).runs += movementResults.runs;
                    });
                });
            }
            if (outs === 3) {
                event.inningEnding = true; // So we can draw a nice slash across the corner of the box.
                baseStatus[0] = null;
                lineScoreMap.get(inning).leftOnBase = baseStatus.filter(base => base).length + (["FO", "FC"].some(code => code == batterReached) ? 1 : 0);
                /* 
                    Fielder's choice/force out is the same as leaving a player on base:
                    https://content.mlb.com/documents/2/2/4/305750224/2019_Official_Baseball_Rules_FINAL_.pdf
                    - See 9.02(g) on page 108
                */
                    outs = 0; baseStatus[0] = null; baseStatus[1] = null; baseStatus[2] = null; baseStatus[3] = null; // Reset outs and bases
            }
            //console.log(`${outs} after box ${event.index}`);
        });
        //console.log(eventPositionMap);

        //console.log(page.events);

        // Draw the table of boxes. If a box has an event, draw it, otherwise, draw a blank box.
        for (let y = 1; y <= rows; y++) {
            const tr = document.createElement('tr');
            tr.className = "game-edit-player-row";

            // Draw player info cell
            const playerCell = document.createElement('td');

            const rowPlayers = page.lineup.filter(le => le.row === y).sort(le => le.firstAB);
            rowPlayers.forEach((rp, index, array) => {
                const playerLine = document.createElement("div");
                playerLine.className = "game-edit-player";

                const playerNumber = document.createElement("div");
                playerNumber.className = "player-number";
                playerNumber.textContent = rp.player.number;
                playerLine.appendChild(playerNumber);
                
                const playerName = document.createElement("a");
                playerName.href = `/Player/${rp.player.shortCode}`;
                if (index < array.length - 1) {
                    playerName.className = "player-substituted";
                }
                playerName.textContent = rp.player.name;
                playerLine.appendChild(playerName);
                
                const playerPosition = document.createElement("div");
                playerPosition.className = "player-position";
                /*  Each position change is separated by semi-colon
                    Format is "position #"/"in box" where in box refers to the other team's box
                    Alternative format for "in box" is "Dn" which refers to a defensive substitution in the opponent's half of the nth inning.
                    - "In box" requires the other team's lineup and events to have been added, thus the alternative
                    - Where the position is offensive (PH/PR/DR), the box # is the current team's box
                    - If "in" is missing, it means either
                        (index === 0): the player started the game at that position OR
                        (index > 0): the player came in for the previous batter's would-have-been next plate appearance box
                    - If "position #" is missing, it means the new player took over at the same position
                    - If both are missing, it implies a pinch hitter that remains in the game
                */
                const positions = rp.positions.split(';');
                if (index === 0) { // Starting position is at positions[0]
                    playerPosition.textContent = Baseball.positionString[positions[0]];
                    // Then show position changes
                }
                else {
                    positions.forEach(position => {
                        const substitution = position.split('/');
                        if (substitution[0]) {
                            playerPosition.textContent += Baseball.positionString[substitution[0]];
                        }
                        if (!isNaN(+substitution[1])) {
                            // Defensive substitution unless substitution[0] is any of "PH" "PR" "DR"
                            // TO DO
                        }
                        else if (/D[1-9]{1}/.test(substitution[1])) {
                            // Defensive substitution
                            // These take place in the top of the inning for the home team, and the bottom for the visitor.
                            playerPosition.textContent += playerPosition.textContent.length > 0 ? " " : ""; // Optional space
                            playerPosition.textContent += this.host ? `(T${substitution[1].charAt(1)})` : `(B${substitution[1].charAt(1)})`;
                            playerPosition.className = "player-substitution-inning";
                        }
                    })
                }
                playerLine.appendChild(playerPosition);
                
                playerCell.appendChild(playerLine);
            });

            tr.appendChild(playerCell);

            // Draw boxes
            for (let x = 1; x < columns; x++) {
                const td = document.createElement('td');
                td.className = "game-edit-player-PA";
                if (eventPositionMap.has(`${x}-${y}`)) {
                    const boxNumber = eventPositionMap.get(`${x}-${y}`);
                    td.id = boxNumber;
                    const event = page.events.find(event => event.index === boxNumber);
                    //console.log(Object.keys(event));
                    td.appendChild(Scorecard.generateDiamondSVG(event));
                }
                else {
                    td.appendChild(Scorecard.generateDiamondSVG(null));
                }

                tr.appendChild(td);
            }

            scorecardTable.appendChild(tr);
        }

        const lineScoreRunsRow = document.createElement('tr');
        const lineScoreHitsRow = document.createElement('tr');
        const lineScoreErrorsRow = document.createElement('tr');
        const lineScoreLOBRow = document.createElement('tr');


        //console.log(lineScoreMap);

        return scorecardTable;
    }

    determineInningStart(page) {
        const inningStartMap = new Map();
        inningStartMap.set(1, 1); // Start of game

        let inning = 1;
        let outs = 0;

        page.events.forEach(event => {
            outs += this.countBaserunningOuts(event.before); // Base runners thrown out before the batter result is determined.
            if (outs === 3) {
                outs = 0;
                inning++;
                inningStartMap.set(inning, event.index); // Batter continues next inning (short At Bat)
            }

            outs += this.batterWasOut(event) ? 1 : 0;
            outs += this.countBaserunningOuts(event.after);
            if (outs === 3) {
                outs = 0;
                inning++;
                inningStartMap.set(inning, event.index + 1); // Next batter starts next inning
            }
            //console.log(`After box ${event.index}, there are ${outs} outs`);
        });

        //console.log(inningStartMap);
        return inningStartMap;
    }

    countBaserunningOuts(event) {
        if (!event) return 0;
        return event.split('x').length - 1;
    }

    batterWasOut(event) {
        // Not out = hit, walk/HBP/catcher interference, fielder error, force out/fielder's choice
        // Special case: strike out, after B-1/[pb/wp]
        if (Baseball.codesNotOut.some(code => event.during.startsWith(code))) {
            return false;
        }
        const codesMaybeOut = ["K", "ꓘ"];
        if (codesMaybeOut.some(code => event.during.startsWith(code)) && event.after?.includes("B-")) {
            return false; // Batter reached on dropped third strike
        }

        // Out = pop/fly/line out, ground out, strike out. Non-strikeouts start with a number.
        if (!isNaN(event.during.charAt(0)) || codesMaybeOut.some(code => event.during.startsWith(code))) {
            return true;
        }

        console.warn(`batterWasOut: could not determine if batter was out for "${event.during}" (box ${index}); returning false`);
        return false;
    }

    static movePlayers(events, event, playerMovement, baseStatus, startingOuts) {
        let movementResults = { outs: 0, runs: 0 }; // Track outs and runs during the movement
        // Validate baserunner even exists...
        const startingBase = playerMovement.charAt(0);
        let error = false;
        switch (startingBase) {
            case '1':
                if (!baseStatus[1]) {
                    error = true;
                    console.error(`Unknown player at first base advanced during ${event.index}`);
                }
                break;
            case '2':
                if (!baseStatus[2]) {
                    error = true;
                    console.error(`Unknown player at second base advanced during ${event.index}`);
                }
                break;
            case '3':
                if (!baseStatus[3]) {
                    error = true;
                    console.error(`Unknown player at third base advanced during ${event.index}`);
                }
                break;
        }
        if (error) {
            return movementResults;
        }

        // Move bases
        const advancedCharacter = playerMovement.charAt(1);
        if (advancedCharacter === '-') { // Successful advancement
            switch (playerMovement) {
                case 'B-1':
                    event.advancement = '1';
                    baseStatus[1] = baseStatus[0]; baseStatus[0] = null;
                    //console.log(`Batter (${event.index}) to first`);
                    break;
                case 'B-2':
                    event.advancement = '2';
                    baseStatus[2] = baseStatus[0]; baseStatus[0] = null;
                    //console.log(`Batter (${event.index}) to second`);
                    break;
                case 'B-3':
                    event.advancement = '3';
                    baseStatus[3] = baseStatus[0]; baseStatus[0] = null;
                    //console.log(`Batter (${event.index}) to third`);
                    break;
                case 'B-H':
                    event.advancement = 'H';
                    movementResults.runs++;
                    baseStatus[0] = null;
                    //console.log(`Batter (${event.index}) to home`);
                    break;
                case '1-2':
                    events.find(event => event.index === baseStatus[1]).advancement = '2';
                    baseStatus[2] = baseStatus[1]; baseStatus[1] = null;
                    //console.log(`Runner on first up to second (${event.index})`);
                    break;
                case '1-3':
                    events.find(event => event.index === baseStatus[1]).advancement = '3';
                    baseStatus[3] = baseStatus[1]; baseStatus[1] = null;
                    //console.log(`Runner on first up to third (${event.index})`);
                    break;
                case '1-H':
                    events.find(event => event.index === baseStatus[1]).advancement = 'H';
                    baseStatus[1] = null;
                    movementResults.runs++;
                    //console.log(`Runner on first scores (${event.index})`);
                    break;
                case '2-3':
                    events.find(event => event.index === baseStatus[2]).advancement = '3';
                    baseStatus[3] = baseStatus[2]; baseStatus[2] = null;
                    //console.log(`Runner on second up to third (${event.index})`);
                    break;
                case '2-H':
                    events.find(event => event.index === baseStatus[2]).advancement = 'H';
                    baseStatus[2] = null;
                    movementResults.runs++;
                    //console.log(`Runner on second scores (${event.index})`);
                    break;
                case '3-H':
                    events.find(event => event.index === baseStatus[3]).advancement = 'H';
                    baseStatus[3] = null;
                    movementResults.runs++;
                    //console.log(`Runner on third scores (${event.index})`);
                    break;
                default:
                    console.error(`Unknown player movement during ${event.index}: ${playerMovement}`);
            }
        }
        else if (advancedCharacter === 'x') { // Out
            movementResults.outs++;
            let baserunner;
            switch (playerMovement) {
                case 'Bx1':
                    events.find(event => event.index === baseStatus[0]).out = startingOuts + movementResults.outs;
                    baseStatus[0] = null;
                    //console.log(`Batter out at first`);
                    break;
                case 'Bx2':
                    events.find(event => event.index === baseStatus[0]).out = startingOuts + movementResults.outs;
                    event.advancement = '1';
                    baseStatus[0] = null;
                    //console.log(`Batter out at second`);
                    break;
                case 'Bx3':
                    events.find(event => event.index === baseStatus[0]).out = startingOuts + movementResults.outs;
                    event.advancement = '2';
                    baseStatus[0] = null;
                    //console.log(`Batter out at third`);
                    break;
                case 'Bx4':
                    events.find(event => event.index === baseStatus[0]).out = startingOuts + movementResults.outs;
                    event.advancement = '3';
                    baseStatus[0] = null;
                    //console.log(`Batter out at home`);
                    break;
                case '1x2':
                    baserunner = events.find(event => event.index === baseStatus[1]);
                    baserunner.out = startingOuts + movementResults.outs;
                    baserunner.advancement = 'x2';
                    baseStatus[1] = null;
                    //console.log(`Runner at first out at second`); 
                    break;
                case '1x3':
                    baserunner = events.find(event => event.index === baseStatus[1]);
                    baserunner.out = startingOuts + movementResults.outs;
                    baserunner.advancement = 'x3';
                    baseStatus[1] = null;
                    //console.log(`Runner at first out at third`); 
                    break;
                case '1xH':
                    baserunner = events.find(event => event.index === baseStatus[1])
                    baserunner.out = startingOuts + movementResults.outs;
                    baserunner.advancement = 'xH';
                    baseStatus[1] = null;
                    //console.log(`Runner at first out at home`);
                    break;
                case '2x3':
                    baserunner = events.find(event => event.index === baseStatus[2])
                    baserunner.out = startingOuts + movementResults.outs;
                    baserunner.advancement = 'x3';
                    baseStatus[2] = null;
                    //console.log(`Runner at second out at third`); 
                    break;
                case '2xH':
                    baserunner = events.find(event => event.index === baseStatus[2])
                    baserunner.out = startingOuts + movementResults.outs;
                    baserunner.advancement = 'xH';
                    baseStatus[2] = null;
                    //console.log(`Runner at second out at home`); 
                    break;
                case '3xH':
                    baserunner = events.find(event => event.index === baseStatus[3])
                    baserunner.out = startingOuts + movementResults.outs;
                    baserunner.advancement = 'xH';
                    baseStatus[3] = null;
                    //console.log(`Runner at third out at home`); 
                    break;
                default:
                    console.error(`Unknown player movement during ${event.index}: ${playerMovement}`);
                    break;
            }
        }
        else {
            console.error(`Error tracking movement for box ${event.index} (before): ${event.before}`);
        }
        return movementResults;
    }

    static generateDiamondSVG(event) {
        const svgNS = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNS, "svg");
        svg.setAttribute("xmlns", svgNS);
        svg.setAttribute("viewBox", "0 0 100 100");
        svg.setAttribute("width", "60");
        svg.setAttribute("height", "60");

        const outline = document.createElementNS(svgNS, "g");
        outline.classList.add("boxscore-diamond-outline");
        if (!event) {
            outline.classList.add("empty-box");
        }

        const outlinePath = document.createElementNS(svgNS, "path");
        outlinePath.setAttribute("d", "M 50 80 L 80 50 A 25 25 0 0 0 20 50 L 50 80");
        outline.appendChild(outlinePath);

        const infieldPath = document.createElementNS(svgNS, "path");
        infieldPath.setAttribute("d", "M 70 60 L 50 40, 30 60");
        outline.appendChild(infieldPath);

        svg.appendChild(outline);

        if (event) {
            const progressPathGroup = document.createElementNS(svgNS, "g");

            // Write event
            const eventText = document.createElementNS(svgNS, "text");
            eventText.setAttribute("text-anchor", "start");
            eventText.setAttribute("alignment-baseline", "hanging");
            eventText.setAttribute("x", "5");
            eventText.setAttribute("y", "5");
            eventText.classList.add("boxscore-text");
            eventText.textContent = Scorecard.toStandardScoringString(event.during);
            if (event.during === "ꓘ") {
                eventText.setAttribute("text-anchor", "end");
                eventText.setAttribute("x", "-5");
                eventText.setAttribute("transform", "scale(-1, 1)");
            }
            svg.appendChild(eventText);

            if (event.rbis) {
                const eventRBIs = document.createElementNS(svgNS, "text");
                eventRBIs.setAttribute("text-anchor", "end");
                eventRBIs.setAttribute("alignment-baseline", "hanging");
                eventRBIs.setAttribute("x", "95");
                eventRBIs.setAttribute("y", "5");
                eventRBIs.classList.add("boxscore-text");
                eventRBIs.textContent = event.rbis > 1 ? `${event.rbis} RBI` : "RBI";
                svg.appendChild(eventRBIs);
            }

            // Draw diamond with event - advancement or out
            const progressPath = document.createElementNS(svgNS, "path");
            switch (event.advancement) {
                case "H":
                    progressPath.classList.add("boxscore-progress-run");
                    progressPath.setAttribute("d", "M 50 80 L 70 60, 50 40, 30 60, 50 80 Z");
                    break;
                case "1":
                    progressPath.setAttribute("d", "M 50 80 L 70 60"); break;
                case "2":
                    progressPath.setAttribute("d", "M 50 80 L 70 60, 50 40"); break;
                case "3":
                    progressPath.setAttribute("d", "M 50 80 L 70 60, 50 40, 30 60"); break;
                case "x2":
                    progressPath.setAttribute("d", "M 50 80 L 70 60, 55 45, 60 50, 55 55, 65 45"); break;
                case "x3":
                    progressPath.setAttribute("d", "M 50 80 L 70 60, 50 40, 35 55, 40 50, 35 45, 45 55"); break;
                case "xH":
                    progressPath.setAttribute("d", "M 50 80 L 70 60, 50 40, 30 60, 45 75, 40 70, 35 75, 45 65"); break;
            }
            if (progressPath.classList.length === 0) {
                progressPath.classList.add("boxscore-progress");
            }

            /* If advancements were a list with events
            -- reason between 1-2: <text transform="rotate(45,0,0)" class="boxscore-text-small" text-anchor="middle" alignment-baseline="middle" x="80" y="-20">6-4</text>
            */

            progressPathGroup.appendChild(progressPath);

            if (event.out) { // Draw Out # and circle it
                const outText = document.createElementNS(svgNS, "text");
                outText.setAttribute("text-anchor", "middle");
                outText.setAttribute("alignment-baseline", "central");
                outText.setAttribute("x", "80");
                outText.setAttribute("y", "80");
                outText.classList.add("boxscore-text");
                outText.textContent = event.out;
                svg.appendChild(outText);

                const outCircle = document.createElementNS(svgNS, "circle");
                outCircle.setAttribute("cx", "80");
                outCircle.setAttribute("cy", "80.5");
                outCircle.setAttribute("r", "13");
                outCircle.classList.add("boxscore-out-circle");
                svg.appendChild(outCircle);
            }
            if (event.inningEnding) { // Draw nice inning ending slash
                const endInningLine = document.createElementNS(svgNS, "path");
                endInningLine.setAttribute("d", "M 85 115 L 115 85");
                endInningLine.classList.add("inning-end-line");
                svg.appendChild(endInningLine);
            }

            svg.appendChild(progressPathGroup);

            /*
                <g class="boxscore-progress">
                    <path class="boxscore-progress" id="1-x2" d="M 50 80 L 70 60, 60 50, 65 45, 55 55, 60 50, 55 45, 70 60" />
                    <path style="display: none" class="boxscore-progress-run" id="1-run" d="M 50 80 L 70 60, 50 40, 30 60, 50 80 Z" />
                    <text transform="rotate(45,0,0)" class="boxscore-text-small" text-anchor="middle" alignment-baseline="middle" x="80" y="-20">6-4</text>
                </g>
            */
        }

        return svg;
    }

    static toStandardScoringString(eventString) {
        if (/^[0-9]{2}/.test(eventString))
            return `${eventString.charAt(0)}-${eventString.charAt(1)}`;
        else if (/^[0-9]{1}\/G/.test(eventString))
            return `${eventString.charAt(0)}U`;
        else if (/^[0-9]{1}\/[FPL]/.test(eventString))
            return `${eventString.charAt(2)}${eventString.charAt(0)}`;
        else if (/^((FO)|(FC))[0-9]{2}/.test(eventString))
            return `${eventString.charAt(2)}-${eventString.charAt(3)}`;
        else if (/^((FO)|(FC))[0-9]{1}/.test(eventString))
            return `${eventString.charAt(2)}U`;
        else if (eventString.startsWith("S"))
            return `1B`;
        else if (eventString.startsWith("D"))
            return `2B`;
        else if (eventString.startsWith("T"))
            return `3B`;
        else if (eventString.startsWith("HR"))
            return `HR`;
        else if (eventString.startsWith("W"))
            return `BB`;
        else if (eventString.startsWith("IW"))
            return `IBB`;
        else if (eventString.startsWith("HBP"))
            return `HBP`;
        else if (eventString.startsWith("K"))
            return `K`;
        else if (eventString.startsWith("ꓘ"))
            return `K`;
        else
            return `*${eventString}`;
    }
}