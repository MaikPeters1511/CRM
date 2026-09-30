import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import {
  EmployeeScheduleComponent,
  mergeSlotsToWorkingHours,
  workingHoursToSelectedSlots,
} from '../../../../Clients/crm-web/src/app/employees/presentation/employee-schedule/employee-schedule.component';
import { EmployeeStateService } from '../../../../Clients/crm-web/src/app/employees/application/employee-state.service';

describe('mergeSlotsToWorkingHours', () => {
  it('merges contiguous hourly slots of a weekday into a single working-hours range (Mo-Fr 09:00-17:00)', () => {
    const selected = new Set(['Monday-9', 'Monday-10', 'Monday-11', 'Monday-16']);

    const slots = mergeSlotsToWorkingHours(selected);

    expect(slots).toEqual([
      { weekday: 'Monday', startTime: '09:00:00', endTime: '12:00:00' },
      { weekday: 'Monday', startTime: '16:00:00', endTime: '17:00:00' },
    ]);
  });

  it('returns an empty list when no slot is selected (Mitarbeiter ohne definierte Arbeitszeiten)', () => {
    expect(mergeSlotsToWorkingHours(new Set())).toEqual([]);
  });
});

describe('workingHoursToSelectedSlots', () => {
  it('expands a working-hours range back into individual hourly slots', () => {
    const selected = workingHoursToSelectedSlots([{ weekday: 'Monday', startTime: '09:00:00', endTime: '12:00:00' }]);
    expect(selected).toEqual(new Set(['Monday-9', 'Monday-10', 'Monday-11']));
  });
});

describe('EmployeeScheduleComponent', () => {
  function setup() {
    const stateMock = {
      selectedEmployee: () => null,
      hasErrors: () => false,
      errors: () => [],
      loadEmployeeById: vi.fn(),
      setWorkingHours: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [EmployeeScheduleComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: EmployeeStateService, useValue: stateMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'e1' } } } },
      ],
    });

    const fixture = TestBed.createComponent(EmployeeScheduleComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
  }

  it('loads the employee schedule on init', () => {
    const { stateMock } = setup();
    expect(stateMock.loadEmployeeById).toHaveBeenCalledWith('e1');
  });

  it('toggles a time slot on click', () => {
    const { component } = setup();
    expect(component.isSelected('Monday', 9)).toBe(false);

    component.toggle('Monday', 9);
    expect(component.isSelected('Monday', 9)).toBe(true);

    component.toggle('Monday', 9);
    expect(component.isSelected('Monday', 9)).toBe(false);
  });

  it('moves focus to the next column on ArrowRight (Tastaturbedienung)', () => {
    const { fixture, component } = setup();
    fixture.detectChanges();
    const first = fixture.nativeElement.querySelector('[data-row="0"][data-col="0"]') as HTMLElement;
    const second = fixture.nativeElement.querySelector('[data-row="0"][data-col="1"]') as HTMLElement;
    const focusSpy = vi.spyOn(second, 'focus');

    component.onKeydown(
      { key: 'ArrowRight', preventDefault: () => {}, currentTarget: first } as unknown as KeyboardEvent,
      0,
      0,
    );

    expect(focusSpy).toHaveBeenCalled();
  });

  it('saves the merged working-hours slots', () => {
    const { component, stateMock } = setup();
    component.toggle('Monday', 9);
    component.toggle('Monday', 10);

    component.save();

    expect(stateMock.setWorkingHours).toHaveBeenCalledWith(
      'e1',
      [{ weekday: 'Monday', startTime: '09:00:00', endTime: '11:00:00' }],
      expect.any(Function),
    );
  });
});
