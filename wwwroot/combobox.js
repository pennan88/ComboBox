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
