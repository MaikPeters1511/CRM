import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AvailableSlot, EmployeeSearchParams, EmployeeSearchResult } from '../domain/employee.model';

/**
 * Infrastructure-Service: kapselt die Aufrufe an `employee-service`, die fuer die
 * Mitarbeiter-/Leistungsauswahl und den Live-Verfuegbarkeits-Check des Terminformulars sowie
 * den Mitarbeiterfilter der Kalenderansicht benoetigt werden (`GET /api/employees`,
 * `GET /api/employees/{id}/availability`). Bewusst schlank/eigenstaendig gehalten (kein
 * Cross-Import aus `employee-web`, ADR 0001).
 */
@Injectable({ providedIn: 'root' })
export class EmployeeApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.employeeApiBaseUrl}/employees`;

  search(params: EmployeeSearchParams): Observable<EmployeeSearchResult> {
    let httpParams = new HttpParams().set('page', params.page);
    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    return this.http.get<EmployeeSearchResult>(this.baseUrl, { params: httpParams });
  }

  getAvailability(employeeId: string, from: string, to: string): Observable<AvailableSlot[]> {
    const httpParams = new HttpParams().set('from', from).set('to', to);
    return this.http.get<AvailableSlot[]>(`${this.baseUrl}/${employeeId}/availability`, {
      params: httpParams,
    });
  }
}
