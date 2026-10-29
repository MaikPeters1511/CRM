import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateServiceRequest,
  Service,
  ServiceSearchParams,
  ServiceSearchResult,
  UpdateServiceRequest,
} from '../domain/service.model';

/**
 * Infrastructure-Service: kapselt alle HTTP-Aufrufe an `catalog-service`.
 * Kein State, keine UI-Logik - reine Anbindung an die REST-API (Gateway-Route
 * `/api/services/**`, siehe UserStories/leistungskatalog.openspec.md Abschnitt 5).
 */
@Injectable({
  providedIn: 'root',
})
export class ServiceCatalogApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.catalogApiBaseUrl}/services`;

  search(params: ServiceSearchParams): Observable<ServiceSearchResult> {
    let httpParams = new HttpParams().set('page', params.page);
    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params.status !== null) {
      httpParams = httpParams.set('status', params.status);
    }
    return this.http.get<ServiceSearchResult>(this.baseUrl, { params: httpParams });
  }

  getById(id: string): Observable<Service> {
    return this.http.get<Service>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateServiceRequest): Observable<Service> {
    return this.http.post<Service>(this.baseUrl, request);
  }

  update(id: string, request: UpdateServiceRequest): Observable<Service> {
    return this.http.put<Service>(`${this.baseUrl}/${id}`, request);
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }
}
