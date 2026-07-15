import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { RowAuditBadge } from './row-audit-badge';
import { RowAuditService } from '@core/services/row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

const trail: RowAuditEntry[] = [
  { dateTime: '2026-06-04T14:30:00', userName: 'alice', actionType: 'Update', actionDesc: 'Title, DisplayOrder' },
  { dateTime: '2026-06-01T09:00:00', userName: 'system', actionType: 'Insert', actionDesc: '課程A' },
];

describe('RowAuditBadge', () => {
  let fixture: ComponentFixture<RowAuditBadge>;
  let component: RowAuditBadge;
  let service: jasmine.SpyObj<RowAuditService>;

  async function setup(rows: RowAuditEntry[]): Promise<void> {
    service = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['history']);
    service.history.and.returnValue(of(rows));

    await TestBed.configureTestingModule({
      imports: [RowAuditBadge],
      providers: [provideNoopAnimations(), { provide: RowAuditService, useValue: service }],
    }).compileComponents();

    fixture = TestBed.createComponent(RowAuditBadge);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('tableName', 'Course');
    fixture.componentRef.setInput('pkid', 123);
    fixture.detectChanges();
  }

  it('fetches this record and shows the most recent change inline', async () => {
    await setup(trail);

    expect(service.history).toHaveBeenCalledWith('Course', 123);
    const summary: HTMLElement = fixture.nativeElement.querySelector('[data-testid="latest-summary"]');
    expect(summary).toBeTruthy();
    expect(summary.textContent).toContain('Update by alice');
    expect(summary.textContent).toContain('2026-06-04 14:30');
  });

  it('opens a dialog listing the full trail newest first', async () => {
    await setup(trail);

    component['open']();
    fixture.detectChanges();

    expect(component['dialogVisible']()).toBeTrue();
    const rows: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('[data-testid="audit-trail"] tbody tr');
    expect(rows.length).toBe(2);
    // Newest first: the Update row precedes the Insert row.
    expect(rows[0].textContent).toContain('Update');
    expect(rows[0].textContent).toContain('alice');
    expect(rows[1].textContent).toContain('Insert');
    expect(rows[1].textContent).toContain('課程A');
  });

  it('shows a neutral empty state when there is no history', async () => {
    await setup([]);

    expect(component['latest']()).toBeNull();
    const empty: HTMLElement = fixture.nativeElement.querySelector('[data-testid="no-history"]');
    expect(empty).toBeTruthy();
    expect(empty.textContent).toContain('No history');

    component['open']();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="dialog-empty"]')).toBeTruthy();
  });
});
