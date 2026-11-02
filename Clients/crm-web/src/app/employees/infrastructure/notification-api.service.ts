import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Notification } from '../domain/notification.model';

/**
 * Infrastructure-Service: kapselt alle HTTP-Aufrufe an die Notification-Endpunkte des
 * `employee-service` (abwesenheit-workflow.openspec.md Abschnitt 5). Kein State, keine
 * UI-Logik - reine Anbindung an die REST-API.
 */
@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.employeeApiBaseUrl}/notifications`;

  /** Eigene Benachrichtigungen, neueste zuerst. */
  getNotifications(): Observable<Notification[]> {
    return this.http.get<Notification[]>(this.baseUrl);
  }

  markAsRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/read`, {});
  }
}
