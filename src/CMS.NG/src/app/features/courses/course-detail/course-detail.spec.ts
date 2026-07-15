import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { of } from 'rxjs';
import { CourseDetail } from './course-detail';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { QrService } from '@core/services/qr.service';
import { Course } from '@core/models/course.model';

const course: Course = {
  pkid: 1, title: 'Azure 基礎', officialTitle: null, courseId: 'C001', prodCourseId: 'P001',
  friendlyUrl: 'azure-basics', displayOrder: 1, partnerPkid: 1, courseGroupPkid: null,
  publishStatusPkid: 1, scheduleOn: '2026-01-01', scheduleOff: '2036-01-01', hour: 8,
  listPrice: 12000, learningCredit: 3.5, material: null, objective: null, target: null,
  prerequisites: null, outline: null, towardCertOrExam: null, note: null, otherInfo: null,
  canRepeat: true, partnerName: 'Microsoft', courseGroupDescription: null,
  publishStatusDescription: '已發布', certificationPkids: [1], jobCategoryPkids: [],
};

const QR_PNG = 'data:image/png;base64,iVBORw0KGgoAAAANS';

describe('CourseDetail', () => {
  let fixture: ComponentFixture<CourseDetail>;
  let component: CourseDetail;
  let service: jasmine.SpyObj<CourseService>;
  let qr: jasmine.SpyObj<QrService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['get']);
    service.get.and.returnValue(of(course));

    const lookups = jasmine.createSpyObj<LookupService>('LookupService', ['certifications', 'jobCategories']);
    lookups.certifications.and.returnValue(of([{ pkid: 1, partnerName: 'Microsoft', title: 'AZ-900' }]));
    lookups.jobCategories.and.returnValue(of([]));

    qr = jasmine.createSpyObj<QrService>('QrService', ['toDataUrl']);
    qr.toDataUrl.and.resolveTo(QR_PNG);

    await TestBed.configureTestingModule({
      imports: [CourseDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookups },
        { provide: QrService, useValue: qr },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the course by numeric id from the route', () => {
    expect(service.get).toHaveBeenCalledWith(1);
    expect(component['course']()?.title).toBe('Azure 基礎');
  });

  it('resolves associated certification labels', () => {
    expect(component['certificationNames']()).toEqual(['Microsoft - AZ-900']);
  });

  it('navigates to edit', () => {
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['edit']();
    expect(nav).toHaveBeenCalledWith(['/courses', 1, 'edit']);
  });

  describe('QR code', () => {
    const expectedUrl = 'https://www.uuu.com.tw/Course/Show/1/C001';

    it('encodes the public URL built from the record pkid and courseId', () => {
      expect(component['qrUrl']()).toBe(expectedUrl);
      expect(qr.toDataUrl).toHaveBeenCalledWith(expectedUrl);
    });

    it('shows the courseId as the QR title', () => {
      const title: HTMLElement = fixture.nativeElement.querySelector('.qr-title');
      expect(title.textContent?.trim()).toBe('C001');
    });

    it('download action produces a downloadable QR image named after the courseId', fakeAsync(() => {
      // Let the QR generation promise resolve so the image data URL is available.
      tick();
      fixture.detectChanges();

      const anchor = document.createElement('a');
      const clickSpy = spyOn(anchor, 'click');
      spyOn(document, 'createElement').and.returnValue(anchor);

      component['downloadQr']();

      expect(document.createElement).toHaveBeenCalledWith('a');
      expect(anchor.href).toContain('data:image/png');
      expect(anchor.download).toBe('C001.png');
      expect(clickSpy).toHaveBeenCalled();
    }));
  });
});
