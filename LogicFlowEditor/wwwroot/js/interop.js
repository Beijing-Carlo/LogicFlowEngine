// Returns the bounding rect of an element in client coordinates.
window.getBoundingRect = function (element) {
    const r = element.getBoundingClientRect();
    return { x: r.left, y: r.top, width: r.width, height: r.height };
};

// Triggers a browser download of a text file.
window.downloadFile = function (filename, content) {
    const blob = new Blob([content], { type: 'application/json' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

// Programmatically clicks a hidden element (used to open the file-picker for load).
window.triggerClick = function (elementId) {
    document.getElementById(elementId)?.click();
};

// Focuses an input element and selects all its text.
window.focusAndSelectInput = function (element) {
    if (element) {
        element.focus();
        element.select();
    }
};

// ── localStorage helpers ──────────────────────────────────────────────
window.saveToLocalStorage = function (key, json) {
    localStorage.setItem(key, json);
};

window.loadFromLocalStorage = function (key) {
    return localStorage.getItem(key);
};

// ── Resize observer for viewport virtualization ───────────────────────
window._resizeObservers = window._resizeObservers || new Map();

window.observeResize = function (element, dotNetRef) {
    if (!element) return;
    var observer = new ResizeObserver(function () {
        dotNetRef.invokeMethodAsync('OnSvgResized');
    });
    observer.observe(element);
    window._resizeObservers.set(element, observer);
};

window.unobserveResize = function (element) {
    if (!element) return;
    var observer = window._resizeObservers.get(element);
    if (observer) {
        observer.disconnect();
        window._resizeObservers.delete(element);
    }
};
