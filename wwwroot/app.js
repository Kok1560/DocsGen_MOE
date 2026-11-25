// Lets a Blazor page send a file to the browser as a download
window.downloadFileFromStream = async (fileName, contentStreamReference) => {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();

    URL.revokeObjectURL(url);
};

// Shows a PDF (sent from the server) inside an <iframe>
window.setPdfPreview = async (iframe, contentStreamReference) => {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: "application/pdf" });
    const url = URL.createObjectURL(blob);

    const oldUrl = iframe.dataset.blobUrl;
    iframe.dataset.blobUrl = url;
    iframe.src = url + "#toolbar=1&navpanes=0";

    // Free the memory of the previous preview a bit later
    if (oldUrl) setTimeout(() => URL.revokeObjectURL(oldUrl), 5000);
};

// Copies text to the clipboard. Falls back to an older method because the modern one
// only works on https or localhost, and an internal site is often plain http.
window.copyText = async (text) => {
    try {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return true;
        }
    } catch { /* use the fallback below */ }

    const box = document.createElement("textarea");
    box.value = text;
    box.style.position = "fixed";
    box.style.opacity = "0";
    document.body.appendChild(box);
    box.select();

    let ok = false;
    try { ok = document.execCommand("copy"); } catch { }
    box.remove();
    return ok;
};

// Editor draft: kept in this browser tab so a language switch (a page reload) does not lose the typing.
// It is only given back when the language button was just clicked (flag set for a few seconds).
window.draft = {
    save: (key, json) => {
        try { sessionStorage.setItem(key, json); } catch { }
    },
    loadIfRestoring: (key) => {
        try {
            const stamp = Number(sessionStorage.getItem("docgen.restore") || 0);
            sessionStorage.removeItem("docgen.restore");
            if (!stamp || Date.now() - stamp > 15000) return null;
            return sessionStorage.getItem(key);
        } catch { return null; }
    }
};

// Sends a saved document's PDF through the system share window (WhatsApp, email, ...).
// target = "whatsapp": if the browser can't share files, download the PDF and open WhatsApp.
window.shareDocumentPdf = async (button, target) => {
    const id = button.dataset.id;
    const name = button.dataset.name || "document";
    const fileName = name + ".pdf";

    button.disabled = true;
    try {
        const response = await fetch(`/download/${id}/pdf`, { credentials: "same-origin" });
        if (!response.ok) throw new Error("PDF not available");

        const blob = await response.blob();
        const file = new File([blob], fileName, { type: "application/pdf" });

        if (navigator.canShare && navigator.canShare({ files: [file] })) {
            await navigator.share({ files: [file], title: name });
            return;
        }

        // This browser can't share files: save the PDF so it can be attached by hand
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 5000);

        if (target === "whatsapp") {
            window.open("https://wa.me/?text=" + encodeURIComponent(name), "_blank", "noopener");
        } else {
            alert("The PDF was downloaded. Attach it in WhatsApp or your email to send it.");
        }
    } catch (e) {
        if (e && e.name === "AbortError") return;   // the person closed the share window: not an error
        alert("Could not prepare the PDF. Please try again.");
    } finally {
        button.disabled = false;
    }
};

// Asks the app (running on this same computer) to put the document's PDF on the Windows clipboard
window.copyDocumentPdf = async (button) => {
    const original = button.innerHTML;
    button.disabled = true;

    let message;
    try {
        const response = await fetch(`/copy/${button.dataset.id}`, { method: "POST", credentials: "same-origin" });
        message = response.ok ? button.dataset.ok : button.dataset.fail;
    } catch {
        message = button.dataset.fail;
    }

    button.textContent = message;
    setTimeout(() => {
        button.innerHTML = original;
        button.disabled = false;
    }, 2000);
};