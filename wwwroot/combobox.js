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
    highlighted.scrollIntoView({ block: 'center', behavior: 'smooth' });
  }
};
