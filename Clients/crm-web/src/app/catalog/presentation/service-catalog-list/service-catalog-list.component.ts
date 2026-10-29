import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ServiceCatalogStateService } from '../../application/service-catalog-state.service';
import { Service } from '../../domain/service.model';

type SortDirection = 'ascending' | 'descending' | 'none';

/**
 * Smart/Container-Component: Leistungsliste mit Suche, Status-Filter, Pagination und
 * Bestaetigungsdialog fuer die Deaktivierung (analog `CustomerListComponent`/`CustomerDetailComponent`).
 */
@Component({
  selector: 'app-service-catalog-list',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './service-catalog-list.component.html',
})
export class ServiceCatalogListComponent implements OnInit {
  protected readonly state = inject(ServiceCatalogStateService);

  private readonly _nameSort = signal<SortDirection>('none');
  protected readonly nameSort = this._nameSort.asReadonly();

  private readonly _deactivateCandidate = signal<Service | null>(null);
  protected readonly deactivateCandidate = this._deactivateCandidate.asReadonly();

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.state.totalCount() / 20)));

  protected readonly sortedServices = computed(() => {
    const services = this.state.services();
    const direction = this._nameSort();
    if (direction === 'none') {
      return services;
    }
    const sorted = [...services].sort((a, b) => a.name.localeCompare(b.name));
    return direction === 'ascending' ? sorted : sorted.reverse();
  });

  ngOnInit(): void {
    this.state.loadServices();
  }

  toggleNameSort(): void {
    const next: SortDirection =
      this._nameSort() === 'ascending' ? 'descending' : this._nameSort() === 'descending' ? 'none' : 'ascending';
    this._nameSort.set(next);
  }

  onSearchChange(value: string): void {
    this.state.setSearchTerm(value);
  }

  onStatusChange(value: string): void {
    const status = value === 'all' ? null : value === 'active';
    this.state.setStatusFilter(status);
  }

  goToPreviousPage(): void {
    if (this.state.page() > 1) {
      this.state.setPage(this.state.page() - 1);
    }
  }

  goToNextPage(): void {
    if (this.state.page() < this.totalPages()) {
      this.state.setPage(this.state.page() + 1);
    }
  }

  statusFilterValue(): 'all' | 'active' | 'inactive' {
    const status = this.state.statusFilter();
    if (status === null) return 'all';
    return status ? 'active' : 'inactive';
  }

  openDeactivateModal(service: Service): void {
    this._deactivateCandidate.set(service);
  }

  closeDeactivateModal(): void {
    this._deactivateCandidate.set(null);
  }

  confirmDeactivate(): void {
    const candidate = this._deactivateCandidate();
    if (!candidate) {
      return;
    }
    this.state.deactivateService(candidate.id, () => this.closeDeactivateModal());
  }
}
