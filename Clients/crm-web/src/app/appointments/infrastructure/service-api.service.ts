import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ServiceLookupItem } from '../domain/service.model';

/**
 * Infrastructure-Service: schlanker Einzel-Lookup einer Leistung (`GET /api/services/{id}`,
 * `catalog-service`), um Termine im Kalender mit dem Leistungsnamen zu beschriften.
 * Bewusst kein Cross-Import aus dem `catalog`-Feature (ADR 0001/0002).
 */
@Injectable({ providedIn: 'root' })
export class ServiceApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.catalogApiBaseUrl}/services`;

  getById(id: string): Observable<ServiceLookupItem> {
    return this.http.get<ServiceLookupItem>(`${this.baseUrl}/${id}`);
  }
}
