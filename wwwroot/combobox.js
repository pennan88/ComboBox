window.comboBoxRegisterOutsideClick = (element, dotNetHelper, portalId, keepFocusOnMouseDown) => {
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
            document.removeEventListener("touchstart", onTouchStart);
            document.removeEventListener("touchmove", blockOverlayTouchMove);
            document.removeEventListener("touchend", onTouchEnd);
            document.removeEventListener("touchcancel", onTouchEnd);
        },
    };
};


window.comboBoxScrollToHighlighted = (root, portalId) => {
    if (!root && !portalId) return;

    const portal = portalId ? document.getElementById(portalId) : null;
    const highlighted =
        (portal && portal.querySelector('.combo-item.highlighted, .add-button.highlighted')) ||
        (root && root.querySelector('.combo-item.highlighted, .add-button.highlighted'));

    if (highlighted) {
        highlighted.scrollIntoView({block: 'center', behavior: 'smooth'});
    }
};

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
    getPosition: function (triggerId, portalId, measureClass) {
        const trigger = document.getElementById(triggerId);
        if (!trigger) return null;

        const portal = portalId ? document.getElementById(portalId) : null;
        const dropdown = portal ? portal.querySelector('.combo-dropdown') : null;
        const gap = 5;

        const preferredHeight = resolvePreferredHeight(dropdown, measureClass);

        const rect = trigger.getBoundingClientRect();
        const spaceBelow = window.innerHeight - rect.bottom - gap;
        const fitsBelow = spaceBelow >= preferredHeight;

        return {
            top: fitsBelow
                ? (rect.bottom + window.scrollY + gap)
                : (rect.top + window.scrollY - gap),
            left: rect.left + window.scrollX,
            width: rect.width,
            placeAbove: !fitsBelow
        };
    }
};

})();
