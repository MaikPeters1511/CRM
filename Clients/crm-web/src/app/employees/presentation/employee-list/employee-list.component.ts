import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EmployeeStateService } from '../../application/employee-state.service';
import { Employee, EmployeeRole } from '../../domain/employee.model';

type SortDirection = 'ascending' | 'descending' | 'none';

/**
 * Smart/Container-Component: Mitarbeiterliste mit Suche, Rollen-/Leistungsfilter
 * und Pagination.
 */
@Component({
  selector: 'app-employee-list',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './employee-list.component.html',
})
export class EmployeeListComponent implements OnInit {
  protected readonly state = inject(EmployeeStateService);

  private readonly _lastNameSort = signal<SortDirection>('none');
  protected readonly lastNameSort = this._lastNameSort.asReadonly();

  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.state.totalCount() / 20)),
  );

  protected readonly sortedEmployees = computed(() => {
    const employees = this.state.employees();
    const direction = this._lastNameSort();
    if (direction === 'none') {
      return employees;
    }
    const sorted = [...employees].sort((a, b) => a.lastName.localeCompare(b.lastName));
    return direction === 'ascending' ? sorted : sorted.reverse();
  });

  ngOnInit(): void {
    this.state.loadEmployees();
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

  onRoleChange(value: string): void {
    this.state.setRoleFilter(value === 'all' ? null : (value as EmployeeRole));
  }

  onServiceIdChange(value: string): void {
    this.state.setServiceIdFilter(value.trim() || null);
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

  roleFilterValue(): 'all' | EmployeeRole {
    return this.state.roleFilter() ?? 'all';
  }

  deactivate(id: string): void {
    this.state.deactivateEmployee(id, () => {});
  }

  serviceNames(employee: Employee): string {
    return employee.services.map((s) => s.serviceName).join(', ');
  }
}
