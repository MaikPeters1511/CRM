import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddAbsenceRequest,
  Absence,
  CreateEmployeeRequest,
  Employee,
  EmployeeSearchParams,
  EmployeeSearchResult,
  PendingAbsence,
  SetWorkingHoursRequest,
  UpdateEmployeeRequest,
} from '../domain/employee.model';

/**
 * Infrastructure-Service: kapselt alle HTTP-Aufrufe an `employee-service`.
 * Kein State, keine UI-Logik - reine Anbindung an die REST-API.
 */
@Injectable({
  providedIn: 'root',
})
export class EmployeeApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.employeeApiBaseUrl}/employees`;

  search(params: EmployeeSearchParams): Observable<EmployeeSearchResult> {
    let httpParams = new HttpParams().set('page', params.page);
    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params.role) {
      httpParams = httpParams.set('role', params.role);
    }
    if (params.serviceId) {
      httpParams = httpParams.set('serviceId', params.serviceId);
    }
    return this.http.get<EmployeeSearchResult>(this.baseUrl, { params: httpParams });
  }

  getById(id: string): Observable<Employee> {
    return this.http.get<Employee>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateEmployeeRequest): Observable<Employee> {
    return this.http.post<Employee>(this.baseUrl, request);
  }

  update(id: string, request: UpdateEmployeeRequest): Observable<Employee> {
    return this.http.put<Employee>(`${this.baseUrl}/${id}`, request);
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  setSchedule(id: string, request: SetWorkingHoursRequest): Observable<Employee> {
    return this.http.put<Employee>(`${this.baseUrl}/${id}/schedule`, request);
  }

  addAbsence(id: string, request: AddAbsenceRequest): Observable<Absence> {
    return this.http.post<Absence>(`${this.baseUrl}/${id}/absences`, request);
  }

  /** Eigene Abwesenheitsanfragen inkl. Status (Mitarbeiter: nur eigene; Admin: beliebige). */
  getAbsences(id: string): Observable<Absence[]> {
    return this.http.get<Absence[]>(`${this.baseUrl}/${id}/absences`);
  }

  /** Alle ausstehenden Anfragen ueber alle Mitarbeiter (Rolle Admin). */
  getPendingAbsences(): Observable<PendingAbsence[]> {
    return this.http.get<PendingAbsence[]>(`${this.baseUrl}/absences/pending`);
  }

  approveAbsence(id: string, absenceId: string): Observable<Absence> {
    return this.http.post<Absence>(`${this.baseUrl}/${id}/absences/${absenceId}/approve`, {});
  }

  rejectAbsence(id: string, absenceId: string): Observable<Absence> {
    return this.http.post<Absence>(`${this.baseUrl}/${id}/absences/${absenceId}/reject`, {});
  }
}
