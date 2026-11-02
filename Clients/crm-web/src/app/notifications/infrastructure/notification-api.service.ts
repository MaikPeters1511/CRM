import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Notification } from '../domain/notification.model';

/**
 * Infrastructure-Service: kapselt die Aufrufe an die Notifications-API, die parallel im
 * Branch `abwesenheit-workflow` entsteht (`GET /api/notifications`,
 * `POST /api/notifications/{id}/read`). Solange der Endpunkt dort noch nicht existiert,
 * liefert der Aufruf 404/503, was `NotificationState` als Leerzustand behandelt.
 */
@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.notificationApiBaseUrl}/notifications`;

  getAll(): Observable<Notification[]> {
    return this.http.get<Notification[]>(this.baseUrl);
  }

  markAsRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/read`, {});
  }
}
