import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CustomerLookupItem, CustomerSearchParams, CustomerSearchResult } from '../domain/customer.model';

/**
 * Infrastructure-Service: kapselt den Kunde-Autocomplete-Aufruf an `customer-service`
 * (`GET /api/customers?search=`). Bewusst schlank/eigenstaendig gehalten (kein Cross-Import
 * aus `customer-web`, ADR 0001).
 */
@Injectable({ providedIn: 'root' })
export class CustomerApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.customerApiBaseUrl}/customers`;

  search(params: CustomerSearchParams): Observable<CustomerSearchResult> {
    let httpParams = new HttpParams().set('page', params.page);
    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params.status !== null) {
      httpParams = httpParams.set('status', params.status);
    }
    return this.http.get<CustomerSearchResult>(this.baseUrl, { params: httpParams });
  }

  // Wird fuer den Deep-Link vom Terminassistenten benoetigt (ki-terminassistent.openspec.md,
  // Abschnitt 4): zeigt den Kundennamen im vorausgefuellten Terminformular an, statt nur die Id.
  getById(id: string): Observable<CustomerLookupItem> {
    return this.http.get<CustomerLookupItem>(`${this.baseUrl}/${id}`);
  }
}
