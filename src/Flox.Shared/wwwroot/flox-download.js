// Gives the user a file made on the page (bytes of .NET arrive as a Uint8Array)
export function download(fileName, contentType, bytes) {
    const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// Opens the file dialog of a hidden <input type="file">
export function pick(input) {
    input.click();
}
