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