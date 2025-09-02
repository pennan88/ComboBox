window.comboBoxRegisterOutsideClick = (element, dotNetHelper) => {
    function handler(e) {
        if (!element.contains(e.target)) {
            dotNetHelper.invokeMethodAsync("CloseDropdown");
        }
    }

    document.addEventListener("click", handler);

    return {
        dispose: () => document.removeEventListener("click", handler),
    };
};


window.comboBoxScrollToHighlighted = (root) => {
    if (!root) return;

    const highlighted = root.querySelector('.combo-item.highlighted');
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
    getPosition: function (triggerId) {
        const trigger = document.getElementById(triggerId);
        if (!trigger) return null;
        const rect = trigger.getBoundingClientRect();
        return {
            top: rect.bottom + window.scrollY +5,
            left: rect.left + window.scrollX,
            width: rect.width
        };
    }
};
