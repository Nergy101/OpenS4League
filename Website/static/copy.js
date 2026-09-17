(() => {
  const copyButtons = document.querySelectorAll("[data-copy-text]");

  async function copyText(text) {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return;
    }

    const input = document.createElement("textarea");
    input.value = text;
    input.setAttribute("readonly", "");
    input.style.position = "fixed";
    input.style.opacity = "0";
    document.body.appendChild(input);
    input.select();
    document.execCommand("copy");
    input.remove();
  }

  copyButtons.forEach((button) => {
    button.addEventListener("click", async () => {
      const originalLabel = button.dataset.originalLabel || button.textContent;
      button.dataset.originalLabel = originalLabel;

      try {
        await copyText(button.dataset.copyText);
        button.textContent = "copied";
        button.classList.add("copied");
        globalThis.setTimeout(() => {
          button.textContent = originalLabel;
          button.classList.remove("copied");
        }, 1800);
      } catch {
        button.textContent = "unable to copy";
        globalThis.setTimeout(() => {
          button.textContent = originalLabel;
        }, 1800);
      }
    });
  });
})();
