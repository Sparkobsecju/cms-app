import { Injectable } from '@angular/core';
import { toDataURL, QRCodeToDataURLOptions } from 'qrcode';

/** Generates QR codes as PNG data URLs. Thin, injectable seam over the `qrcode` library. */
@Injectable({ providedIn: 'root' })
export class QrService {
  /** Encode `value` into a `data:image/png;base64,…` URL suitable for `<img src>` and download. */
  toDataUrl(value: string, options?: QRCodeToDataURLOptions): Promise<string> {
    return toDataURL(value, { margin: 1, width: 220, ...options });
  }
}
