import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppointmentHistoryEntry } from '../domain/appointment-history.model';

/**
 * Infrastructure-Service: kapselt den Aufruf an `appointment-service`
 * (`GET /api/appointments?customerId=`), um die vollstaendige Terminhistorie eines Kunden in
 * der Kundendetailsicht anzuzeigen. Bewusst schlank/eigenstaendig gehalten (kein Cross-Import
 * aus `appointments`, ADR 0001).
 */
@Injectable({ providedIn: 'root' })
export class AppointmentHistoryApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.appointmentApiBaseUrl}/appointments`;

  getForCustomer(customerId: string, from: string, to: string): Observable<AppointmentHistoryEntry[]> {
    const params = new HttpParams().set('customerId', customerId).set('from', from).set('to', to);
    return this.http
      .get<AppointmentHistoryEntry[] | { appointments: AppointmentHistoryEntry[] }>(this.baseUrl, { params })
      .pipe(map((res) => (Array.isArray(res) ? res : res.appointments)));
  }
}
