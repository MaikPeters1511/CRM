import { Injectable, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { AppointmentListItem } from '../domain/appointment.model';
import { CustomerApiService } from '../infrastructure/customer-api.service';
import { ServiceApiService } from '../infrastructure/service-api.service';

/**
 * Application-Layer: loest Kunden- und Leistungsnamen fuer die im Kalender sichtbaren Termine
 * auf (`AppointmentListItem` traegt nur Ids, Database-per-Service). Jede Id wird hoechstens
 * einmal abgefragt (Cache inkl. fehlgeschlagener Abfragen, damit z.B. ein fuer `Employee`
 * nicht lesbarer Kunde nicht endlos neu angefragt wird); Fehler lassen die Id unaufgeloest.
 */
@Injectable({ providedIn: 'root' })
export class AppointmentLabelsService {
  private readonly customerApi = inject(CustomerApiService);
  private readonly serviceApi = inject(ServiceApiService);

  private readonly _customerNames = signal<ReadonlyMap<string, string>>(new Map());
  private readonly _serviceNames = signal<ReadonlyMap<string, string>>(new Map());
  private readonly requestedCustomers = new Set<string>();
  private readonly requestedServices = new Set<string>();

  customerName(customerId: string): string | null {
    return this._customerNames().get(customerId) ?? null;
  }

  serviceName(serviceId: string): string | null {
    return this._serviceNames().get(serviceId) ?? null;
  }

  ensureLoaded(appointments: readonly AppointmentListItem[]): void {
    for (const customerId of new Set(appointments.map((a) => a.customerId))) {
      if (this.requestedCustomers.has(customerId)) {
        continue;
      }
      this.requestedCustomers.add(customerId);
      this.customerApi
        .getById(customerId)
        .pipe(catchError(() => of(null)))
        .subscribe((customer) => {
          if (customer) {
            this._customerNames.update((m) => new Map(m).set(customerId, `${customer.firstName} ${customer.lastName}`));
          }
        });
    }
    for (const serviceId of new Set(appointments.map((a) => a.serviceId))) {
      if (this.requestedServices.has(serviceId)) {
        continue;
      }
      this.requestedServices.add(serviceId);
      this.serviceApi
        .getById(serviceId)
        .pipe(catchError(() => of(null)))
        .subscribe((service) => {
          if (service) {
            this._serviceNames.update((m) => new Map(m).set(serviceId, service.name));
          }
        });
    }
  }
}
