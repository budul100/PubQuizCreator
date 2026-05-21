// Select all text when focusing any input or textarea
document.addEventListener("focusin", function (e) {
    if (e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA") {
        e.target.select();
    }
});

// Returns the current inner width of the browser window.
window.getWindowWidth = () => window.innerWidth;

function getAutoOpenCapture() {
    return localStorage.getItem("autoOpenCapture") === "true";
}

function setAutoOpenCapture(value) {
    localStorage.setItem("autoOpenCapture", value ? "true" : "false");
}

function markInternalNavigation() {
    sessionStorage.setItem("internalNav", "true");
}

function consumeInternalNavigation() {
    const was = sessionStorage.getItem("internalNav") === "true";
    sessionStorage.removeItem("internalNav");
    return was;
}
