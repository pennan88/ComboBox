window.comboBoxRegisterOutsideClick = (element, dotNetHelper, portalId) => {
    let lastTouchY = null;

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
    document.addEventListener("wheel", blockOverlayWheel, { passive: false });
    document.addEventListener("touchstart", onTouchStart, { passive: true });
    document.addEventListener("touchmove", blockOverlayTouchMove, { passive: false });
    document.addEventListener("touchend", onTouchEnd, { passive: true });
    document.addEventListener("touchcancel", onTouchEnd, { passive: true });

    return {
        dispose: () => {
            document.removeEventListener("click", handler);
            document.removeEventListener("wheel", blockOverlayWheel);
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
        (portal && portal.querySelector('.combo-item.highlighted')) ||
        (root && root.querySelector('.combo-item.highlighted'));

    if (highlighted) {
        highlighted.scrollIntoView({block: 'center', behavior: 'smooth'});
    }
};

window.portalHelper = {
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
            if (el && el.remove) {
                el.remove(); 
            } else {
                console.warn("removeFromBody: element not found or can't be removed", {el, id});
            }
        } catch (err) {
            console.error("removeFromBody error:", err);
        }
    },
    getPosition: function (triggerId, portalId) {
        const trigger = document.getElementById(triggerId);
        if (!trigger) return null;

        const portal = portalId ? document.getElementById(portalId) : null;
        const dropdown = portal ? portal.querySelector('.combo-dropdown') : null;
        const gap = 5;

        const maxHeightCss = dropdown ? window.getComputedStyle(dropdown).maxHeight : null;
        const preferredHeight = Number.parseFloat(maxHeightCss || '') || 320;

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
