document.addEventListener("DOMContentLoaded", () => {
    const root = document.documentElement;
    const btn = document.getElementById("theme-toggle");
    const saved = localStorage.getItem("reth-theme");
    const initial = saved || "dark";
    root.setAttribute("data-theme", initial);

    btn?.addEventListener("click", () => {
        const next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
        root.setAttribute("data-theme", next);
        localStorage.setItem("reth-theme", next);
    });
});
