/** Triggers a browser "save as" download for a Blob under the given filename. */
export function saveBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  // Revoke on the next tick: the download starts asynchronously, so revoking synchronously after
  // click() can invalidate the URL before the browser begins fetching it and abort the download.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
