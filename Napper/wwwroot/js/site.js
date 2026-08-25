window.napperHome = {
    switchLogTab(tabKey) {
        const host = document.getElementById("logga");
        if (!host) {
            return;
        }

        const forms = host.querySelectorAll("[data-log-form]");
        const buttons = host.querySelectorAll("[data-log-tab-button]");

        forms.forEach((form) => {
            const isActive = form.getAttribute("data-log-form") === tabKey;
            form.classList.toggle("log-form-hidden", !isActive);
        });

        buttons.forEach((button) => {
            const isActive = button.getAttribute("data-log-tab-button") === tabKey;
            button.classList.toggle("is-active", isActive);
        });
    }
};
