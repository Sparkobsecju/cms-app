import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { CourseGroupDetail } from './course-group-detail';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

const group: CourseGroup = { pkid: 1, description: '資訊技術' };

describe('CourseGroupDetail', () => {
  let fixture: ComponentFixture<CourseGroupDetail>;
  let component: CourseGroupDetail;
  let service: jasmine.SpyObj<CourseGroupService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['get']);
    service.get.and.returnValue(of(group));

    await TestBed.configureTestingModule({
      imports: [CourseGroupDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: CourseGroupService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the group by numeric id from the route', () => {
    expect(service.get).toHaveBeenCalledWith(1);
    expect(component['group']()?.description).toBe('資訊技術');
  });

  it('navigates to edit', () => {
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['edit']();
    expect(nav).toHaveBeenCalledWith(['/course-groups', 1, 'edit']);
  });
});
