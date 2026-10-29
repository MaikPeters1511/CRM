import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ActiveService } from '../domain/active-service.model';

/**
 * Infrastructure-Service: schlanker, eigenstaendiger Aufruf an `catalog-service`
 * (`GET /api/services?status=true`), um die Leistungsauswahl im Mitarbeiterformular zu
 * befuellen. Bewusst kein Cross-Import aus dem `catalog`-Feature-Bereich (analog
 * `appointments/infrastructure/employee-api.service.ts`, ADR 0001).
 */
@Injectable({ providedIn: 'root' })
export class ServiceLookupApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.catalogApiBaseUrl}/services`;

  getActiveServices(): Observable<ActiveService[]> {
    const params = new HttpParams().set('status', true);
    return this.http
      .get<{ items: { id: string; name: string }[] }>(this.baseUrl, { params })
      .pipe(map((result) => result.items.map((item) => ({ id: item.id, name: item.name }))));
  }
}
