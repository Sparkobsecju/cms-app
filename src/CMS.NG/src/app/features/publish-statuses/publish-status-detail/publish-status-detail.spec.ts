import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PublishStatusDetail } from './publish-status-detail';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

const status: PublishStatus = {
  pkid: 1, description: 'Draft', isDraft: true, isPublished: false, isDiscontinued: false,
};

describe('PublishStatusDetail', () => {
  let fixture: ComponentFixture<PublishStatusDetail>;
  let component: PublishStatusDetail;
  let service: jasmine.SpyObj<PublishStatusService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['get']);
    service.get.and.returnValue(of(status));

    await TestBed.configureTestingModule({
      imports: [PublishStatusDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PublishStatusService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the status by numeric id from the route', () => {
    expect(service.get).toHaveBeenCalledWith(1);
    expect(component['status']()?.description).toBe('Draft');
  });

  it('navigates to edit', () => {
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['edit']();
    expect(nav).toHaveBeenCalledWith(['/publish-statuses', 1, 'edit']);
  });
});
