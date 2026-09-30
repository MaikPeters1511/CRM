import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateCustomerRequest,
  Customer,
  CustomerSearchParams,
  CustomerSearchResult,
  UpdateCustomerRequest,
} from '../domain/customer.model';

/**
 * Infrastructure-Service: kapselt alle HTTP-Aufrufe an `customer-service`.
 * Kein State, keine UI-Logik - reine Anbindung an die REST-API.
 */
@Injectable({
  providedIn: 'root',
})
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

  getById(id: string): Observable<Customer> {
    return this.http.get<Customer>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateCustomerRequest): Observable<Customer> {
    return this.http.post<Customer>(this.baseUrl, request);
  }

  update(id: string, request: UpdateCustomerRequest): Observable<Customer> {
    return this.http.put<Customer>(`${this.baseUrl}/${id}`, request);
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }
}
