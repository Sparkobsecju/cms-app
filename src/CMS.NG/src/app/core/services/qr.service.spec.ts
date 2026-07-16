import { TestBed } from '@angular/core/testing';
import { QrService } from './qr.service';

describe('QrService', () => {
  let service: QrService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(QrService);
  });

  it('encodes a value into a PNG image data URL', async () => {
    const url = 'https://www.uuu.com.tw/Course/Show/1/C001';
    const dataUrl = await service.toDataUrl(url);
    expect(dataUrl.startsWith('data:image/png;base64,')).toBeTrue();
    expect(dataUrl.length).toBeGreaterThan('data:image/png;base64,'.length);
  });

  it('produces different images for different values', async () => {
    const a = await service.toDataUrl('https://www.uuu.com.tw/Course/Show/1/C001');
    const b = await service.toDataUrl('https://www.uuu.com.tw/Course/Show/2/C002');
    expect(a).not.toBe(b);
  });
});
