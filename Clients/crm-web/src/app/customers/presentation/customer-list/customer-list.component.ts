import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CustomerStateService } from '../../application/customer-state.service';

type SortDirection = 'ascending' | 'descending' | 'none';

/**
 * Smart/Container-Component: Kundenliste mit Suche, Status-Filter und Pagination.
 */
@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './customer-list.component.html',
})
export class CustomerListComponent implements OnInit {
  protected readonly state = inject(CustomerStateService);

  private readonly _lastNameSort = signal<SortDirection>('none');
  protected readonly lastNameSort = this._lastNameSort.asReadonly();

  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.state.totalCount() / 20)),
  );

  protected readonly sortedCustomers = computed(() => {
    const customers = this.state.customers();
    const direction = this._lastNameSort();
    if (direction === 'none') {
      return customers;
    }
    const sorted = [...customers].sort((a, b) => a.lastName.localeCompare(b.lastName));
    return direction === 'ascending' ? sorted : sorted.reverse();
  });

  ngOnInit(): void {
    this.state.loadCustomers();
  }

  toggleLastNameSort(): void {
    const next: SortDirection =
      this._lastNameSort() === 'ascending'
        ? 'descending'
        : this._lastNameSort() === 'descending'
          ? 'none'
          : 'ascending';
    this._lastNameSort.set(next);
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
}
