(() => {
    const key = "studyflow.workspace.v1";

    window.studyFlow = {
        read() {
            const now = new Date();
            const month = String(now.getMonth() + 1).padStart(2, "0");
            const day = String(now.getDate()).padStart(2, "0");
            return { today: now.getFullYear() + "-" + month + "-" + day, raw: localStorage.getItem(key) };
        },

        save(expected, next) {
            const write = () => {
                if (localStorage.getItem(key) !== expected) return false;
                localStorage.setItem(key, next);
                return true;
            };

            return navigator.locks ? navigator.locks.request(key, write) : write();
        },

        dateValue(input) {
            return input.value;
        },

        download(content, name) {
            const url = URL.createObjectURL(new Blob([content], { type: "application/json" }));
            const link = document.createElement("a");
            link.href = url;
            link.download = name;
            document.body.append(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        },

        openDialog(dialog) {
            dialog.addEventListener("cancel", event => {
                event.preventDefault();
                dialog.querySelector("[data-close]").click();
            });
            dialog.showModal();
        },

        closeDialog() {
            document.querySelector(".planner-dialog[open]")?.close();
        }
    };
})();
