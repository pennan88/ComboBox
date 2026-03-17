window.comboBoxRegisterOutsideClick = (element, dotNetHelper, portalId) => {
    function handler(e) {
        const portal = portalId ? document.getElementById(portalId) : null;
        const clickedInsideRoot = !!element && element.contains(e.target);
        const clickedInsidePortal = !!portal && portal.contains(e.target);

        if (!clickedInsideRoot && !clickedInsidePortal) {
            dotNetHelper.invokeMethodAsync("CloseDropdown");
        }
    }

    document.addEventListener("click", handler);

    return {
        dispose: () => document.removeEventListener("click", handler),
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
