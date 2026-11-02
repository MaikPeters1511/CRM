import { Injectable, inject, signal } from '@angular/core';
import { catchError, finalize, forkJoin, of, tap } from 'rxjs';
import { AppointmentApiService } from '../../appointments/infrastructure/appointment-api.service';
import { CustomerApiService } from '../../customers/infrastructure/customer-api.service';
import { EmployeeApiService } from '../../employees/infrastructure/employee-api.service';
import { DashboardMetrics, UpcomingAppointmentItem } from '../domain/dashboard.model';

const UPCOMING_LIMIT = 5;

function todayDateOnly(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Application-Layer: aggregiert Dashboard-Kennzahlen und die anstehenden Termine aus den
 * bereits bestehenden `AppointmentApiService`/`CustomerApiService`/`EmployeeApiService`
 * (keine neuen Backend-Endpunkte, siehe UserStories/mp-crm-mockup-redesign.openspec.md).
 * Die rollenbasierte Sichtbarkeit von Terminen (Employee sieht nur eigene, Admin alle) wird
 * bereits serverseitig durch `GET /api/appointments` erzwungen
 * (appointment-scheduling.openspec.md, Requirement "Sichtbarkeit von Terminen nach Rolle").
 */
@Injectable({ providedIn: 'root' })
export class DashboardStateService {
  private readonly appointmentApi = inject(AppointmentApiService);
  private readonly customerApi = inject(CustomerApiService);
  private readonly employeeApi = inject(EmployeeApiService);

  private readonly _metrics = signal<DashboardMetrics | null>(null);
  private readonly _upcomingAppointments = signal<UpcomingAppointmentItem[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly metrics = this._metrics.asReadonly();
  readonly upcomingAppointments = this._upcomingAppointments.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  load(): void {
    this._loading.set(true);
    this._error.set(null);
    const today = todayDateOnly();

    forkJoin({
      appointments: this.appointmentApi.getCalendar({
        employeeId: null,
        customerId: null,
        from: today,
        to: today,
      }),
      customers: this.customerApi.search({ search: null, status: true, page: 1 }),
      employees: this.employeeApi.search({ search: null, role: null, serviceId: null, page: 1 }),
    })
      .pipe(
        tap(({ appointments, customers, employees }) => {
          const active = appointments.filter((appointment) => appointment.status !== 'Cancelled');

          this._metrics.set({
            appointmentsToday: active.length,
            openRequests: appointments.filter((appointment) => appointment.status === 'RequiresReview')
              .length,
            activeCustomers: customers.totalCount,
            totalEmployees: employees.totalCount,
          });

          this._upcomingAppointments.set(
            [...active]
              .sort((a, b) => a.start.localeCompare(b.start))
              .slice(0, UPCOMING_LIMIT)
              .map((appointment) => ({
                id: appointment.id,
                start: appointment.start,
                end: appointment.end,
                status: appointment.status,
              })),
          );
        }),
        catchError(() => {
          this._metrics.set(null);
          this._upcomingAppointments.set([]);
          this._error.set('dashboard.errors.generic');
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }
}
