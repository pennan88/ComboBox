window.comboBoxRegisterOutsideClick = (element, dotNetHelper, portalId, keepFocusOnMouseDown, multiSelect) => {
    let lastTouchY = null;
    const scrollKeys = new Set(['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown']);

    function getPortal() {
        return portalId ? document.getElementById(portalId) : null;
    }

    function handler(e) {
        const portal = getPortal();
        const clickedInsideRoot = !!element && element.contains(e.target);
        const clickedInsidePortal = !!portal && portal.contains(e.target);

        if (!clickedInsideRoot && !clickedInsidePortal) {
            dotNetHelper.invokeMethodAsync("CloseDropdown");
        }
    }

    function canScroll(container, deltaY) {
        if (!container) return false;

        if (deltaY > 0) {
            return container.scrollTop + container.clientHeight < container.scrollHeight - 1;
        }

        if (deltaY < 0) {
            return container.scrollTop > 0;
        }

        return true;
    }

    function shouldBlockScroll(target, deltaY) {
        const portal = getPortal();
        if (!portal) return false;

        const dropdown = portal.querySelector('.combo-dropdown');
        const insideDropdown = !!dropdown && dropdown.contains(target);

        if (!insideDropdown) {
            return true;
        }

        if (!(target instanceof Element)) {
            return true;
        }

        const scrollContainer = target.closest('.combo-content-container');
        if (!scrollContainer || !dropdown.contains(scrollContainer)) {
            return true;
        }

        return !canScroll(scrollContainer, deltaY);
    }

    function blockOverlayWheel(e) {
        if (shouldBlockScroll(e.target, e.deltaY)) {
            e.preventDefault();
        }
    }

    function blockOverlayKeyDown(e) {
        if (!scrollKeys.has(e.key)) {
            return;
        }

        if (getPortal()) {
            e.preventDefault();
        }
    }

    // Blazor's @onmousedown:preventDefault does not reach the dropdown once the portal has
    // been moved to <body>, so focus is kept in the trigger input from here instead: a click
    // on a row, header or footer button must not blur the input the user is typing in.
    function keepFocus(e) {
        const portal = getPortal();
        const dropdown = portal && portal.querySelector('.combo-dropdown');
        if (!dropdown || !(e.target instanceof Element) || !dropdown.contains(e.target)) {
            return;
        }

        // Form fields placed in a template still need to take focus when clicked.
        if (e.target.closest('input, textarea, select, [contenteditable]')) {
            return;
        }

        e.preventDefault();
    }

    // In multi-select mode Space toggles the highlighted option (handled in C#). Its default
    // action has to go: on the Select trigger it would click the button and close the dropdown,
    // and in the empty search input it would type a space and start a search.
    function cancelToggleSpace(e) {
        const portal = getPortal();
        if (e.key !== ' ' || !portal || !(e.target instanceof Element)) {
            return;
        }

        const onSelectTrigger = !!element && element.contains(e.target) && e.target.matches('.select-trigger');
        const inEmptySearch = portal.contains(e.target) && e.target.matches('.combo-search-input') && e.target.value === '';

        if (onSelectTrigger || inEmptySearch) {
            e.preventDefault();
        }
    }

    // The mouse makes a row the active option only when it really moves. A row that slides under a
    // still pointer while the keyboard scrolls the list must not steal the highlight, so this uses
    // mousemove (with a position check, as some browsers fire it after scrolling) rather than
    // mouseenter/:hover, and only calls .NET when the row under the pointer is not highlighted yet.
    let lastPointerX = null;
    let lastPointerY = null;
    function highlightOnPointerMove(e) {
        if (e.screenX === lastPointerX && e.screenY === lastPointerY) {
            return;
        }
        lastPointerX = e.screenX;
        lastPointerY = e.screenY;

        const portal = getPortal();
        if (!portal || !(e.target instanceof Element) || !portal.contains(e.target)) {
            return;
        }

        const row = e.target.closest('.combo-item[role="option"]');
        if (!row || !row.id || row.classList.contains('highlighted') || row.classList.contains('combo-item-disabled')) {
            return;
        }

        dotNetHelper.invokeMethodAsync("HighlightOptionFromPointer", row.id);
    }

    function onTouchStart(e) {
        if (e.touches.length > 0) {
            lastTouchY = e.touches[0].clientY;
        }
    }

    function blockOverlayTouchMove(e) {
        if (e.touches.length === 0 || lastTouchY === null) {
            return;
        }

        const currentY = e.touches[0].clientY;
        const deltaY = lastTouchY - currentY;
        lastTouchY = currentY;

        if (shouldBlockScroll(e.target, deltaY)) {
            e.preventDefault();
        }
    }

    function onTouchEnd() {
        lastTouchY = null;
    }

    document.addEventListener("click", handler);
    if (keepFocusOnMouseDown) {
        document.addEventListener("mousedown", keepFocus);
    }
    document.addEventListener("wheel", blockOverlayWheel, { passive: false });
    document.addEventListener("keydown", blockOverlayKeyDown);
    if (multiSelect) {
        document.addEventListener("keydown", cancelToggleSpace);
    }
    document.addEventListener("mousemove", highlightOnPointerMove, { passive: true });
    document.addEventListener("touchstart", onTouchStart, { passive: true });
    document.addEventListener("touchmove", blockOverlayTouchMove, { passive: false });
    document.addEventListener("touchend", onTouchEnd, { passive: true });
    document.addEventListener("touchcancel", onTouchEnd, { passive: true });

    return {
        dispose: () => {
            document.removeEventListener("click", handler);
            document.removeEventListener("mousedown", keepFocus);
            document.removeEventListener("wheel", blockOverlayWheel);
            document.removeEventListener("keydown", blockOverlayKeyDown);
            document.removeEventListener("keydown", cancelToggleSpace);
            document.removeEventListener("mousemove", highlightOnPointerMove);
            document.removeEventListener("touchstart", onTouchStart);
            document.removeEventListener("touchmove", blockOverlayTouchMove);
            document.removeEventListener("touchend", onTouchEnd);
            document.removeEventListener("touchcancel", onTouchEnd);
        },
    };
};


window.comboBoxScrollToHighlighted = (root, portalId, optionId) => {
    if (!root && !portalId) return;

    // The option id comes from .NET, so the right row is found even if the render batch that moves
    // the .highlighted class has not been applied yet (fast key repeat).
    const portal = portalId ? document.getElementById(portalId) : null;
    const highlighted =
        (optionId && document.getElementById(optionId)) ||
        (portal && portal.querySelector('.combo-item.highlighted, .add-button.highlighted')) ||
        (root && root.querySelector('.combo-item.highlighted, .add-button.highlighted'));

    if (!highlighted) return;

    // Scroll the results list only. scrollIntoView() would also scroll every scrollable ancestor,
    // including the page itself, whenever the dropdown was not already centred in the window.
    const container = highlighted.closest('.combo-content-container');
    if (!container) return;

    // Moving up onto the first row of a group should also reveal the group's header.
    const header = highlighted.previousElementSibling;
    const top = (header && header.classList.contains('combo-group-header') ? header : highlighted).getBoundingClientRect().top;
    const bottom = highlighted.getBoundingClientRect().bottom;
    const box = container.getBoundingClientRect();

    if (top < box.top) {
        container.scrollTop -= box.top - top;
    } else if (bottom > box.bottom) {
        container.scrollTop += bottom - box.bottom;
    }
};

// The arrow key that opens a closed dropdown arrives before the dropdown registers its own key
// handling (comboBoxRegisterOutsideClick), so its default action would scroll the page, and in a
// text input move the caret. Arrow keys on a combobox trigger or search input never do either.
document.addEventListener("keydown", (e) => {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    if (!(e.target instanceof Element)) return;

    if (e.target.matches('.combo-container [role="combobox"], .combo-search-input')) {
        e.preventDefault();
    }
});

window.portalHelper = (function () {

function resolvePreferredHeight(dropdown, measureClass) {
    const fallback = 320;
    const readMaxHeight = (el) => Number.parseFloat(window.getComputedStyle(el).maxHeight);

    if (dropdown) {
        const measured = readMaxHeight(dropdown);
        if (Number.isFinite(measured) && measured > 0) {
            return measured;
        }
    }

    // The dropdown has not rendered yet when the portal is positioned (Portal only
    // renders its content once it has been appended to <body>), so measure a throwaway
    // probe carrying the same classes. It is appended to <body> because that is where
    // the real dropdown ends up, so contextual selectors resolve the same way.
    const probe = document.createElement('div');
    probe.className = measureClass || 'combo-dropdown';
    probe.style.cssText = 'position:absolute;visibility:hidden;pointer-events:none;top:-9999px;left:-9999px';
    document.body.appendChild(probe);

    try {
        const measured = readMaxHeight(probe);
        return Number.isFinite(measured) && measured > 0 ? measured : fallback;
    } catch (err) {
        console.error("resolvePreferredHeight error:", err);
        return fallback;
    } finally {
        probe.remove();
    }
}

function resolveWidth(triggerWidth, options) {
    const probe = document.createElement('div');
    probe.style.cssText = 'position:absolute;visibility:hidden;pointer-events:none;top:-9999px;left:-9999px;box-sizing:border-box';
    probe.style.width = options.extra ? `calc(${triggerWidth}px + ${options.extra})` : `${triggerWidth}px`;
    if (options.minWidth) probe.style.minWidth = options.minWidth;
    if (options.maxWidth) probe.style.maxWidth = options.maxWidth;
    document.body.appendChild(probe);

    try {
        const measured = probe.getBoundingClientRect().width;
        return Number.isFinite(measured) && measured > 0 ? measured : triggerWidth;
    } finally {
        probe.remove();
    }
}

return {
    appendToBody: function (id) {
        try {
            const el = document.getElementById(id);
            if (el && document.body && el.parentNode !== document.body) {
                document.body.appendChild(el);
            }
        } catch (err) {
            console.error("appendToBody error:", err);
        }
    },
    removeFromBody: function (id) {
        try {
            const el = document.getElementById(id);
            // Blazor removes the portal node itself when the component is torn down,
            // so "already gone" is the normal case here, not a problem worth reporting.
            if (el && el.remove) {
                el.remove();
            }
        } catch (err) {
            console.error("removeFromBody error:", err);
        }
    },
    getPosition: function (triggerId, portalId, measureClass, widthOptions) {
        const trigger = document.getElementById(triggerId);
        if (!trigger) return null;

        const portal = portalId ? document.getElementById(portalId) : null;
        const dropdown = portal ? portal.querySelector('.combo-dropdown') : null;
        const gap = 5;

        const preferredHeight = resolvePreferredHeight(dropdown, measureClass);

        const rect = trigger.getBoundingClientRect();
        const spaceBelow = window.innerHeight - rect.bottom - gap;
        const fitsBelow = spaceBelow >= preferredHeight;
        const top = fitsBelow
            ? (rect.bottom + window.scrollY + gap)
            : (rect.top + window.scrollY - gap);

        if (!widthOptions) {
            return {
                top: top,
                left: rect.left + window.scrollX,
                width: rect.width,
                placeAbove: !fitsBelow
            };
        }

        // DropdownMinWidth / DropdownMaxWidth: resolve the width in pixels (CSS lengths such as rem
        // or vw resolve on a probe), never narrower than the trigger, then anchor it and keep it
        // inside the viewport horizontally.
        const width = Math.max(rect.width, resolveWidth(rect.width, widthOptions));
        const viewportWidth = document.documentElement.clientWidth || window.innerWidth;
        const margin = 8;

        let left;
        switch (widthOptions.anchor) {
            case 'Right':
                left = rect.right - width;
                break;
            case 'Center':
                left = rect.left + rect.width / 2 - width / 2;
                break;
            default:
                left = rect.left;
        }

        left = Math.min(left, viewportWidth - margin - width);
        left = Math.max(left, margin);

        return {
            top: top,
            left: left + window.scrollX,
            width: width,
            placeAbove: !fitsBelow,
            resolved: true
        };
    }
};

})();
