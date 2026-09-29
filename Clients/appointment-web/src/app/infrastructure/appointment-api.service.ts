import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Appointment,
  AppointmentListItem,
  CalendarQueryParams,
  CancelAppointmentRequest,
  CreateAppointmentRequest,
  RescheduleAppointmentRequest,
} from '../domain/appointment.model';

/**
 * Infrastructure-Service: kapselt alle HTTP-Aufrufe an `appointment-service`
 * (terminverwaltung.openspec.md Abschnitt 5).
 */
@Injectable({ providedIn: 'root' })
export class AppointmentApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.appointmentApiBaseUrl}/appointments`;

  getCalendar(params: CalendarQueryParams): Observable<AppointmentListItem[]> {
    let httpParams = new HttpParams().set('from', params.from).set('to', params.to);
    if (params.employeeId) {
      httpParams = httpParams.set('employeeId', params.employeeId);
    }
    if (params.customerId) {
      httpParams = httpParams.set('customerId', params.customerId);
    }
    return this.http.get<AppointmentListItem[]>(this.baseUrl, { params: httpParams });
  }

  getById(id: string): Observable<Appointment> {
    return this.http.get<Appointment>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateAppointmentRequest): Observable<Appointment> {
    return this.http.post<Appointment>(this.baseUrl, request);
  }

  reschedule(id: string, request: RescheduleAppointmentRequest): Observable<Appointment> {
    return this.http.put<Appointment>(`${this.baseUrl}/${id}/reschedule`, request);
  }

  cancel(id: string, request: CancelAppointmentRequest): Observable<Appointment> {
    return this.http.post<Appointment>(`${this.baseUrl}/${id}/cancel`, request);
  }
}
