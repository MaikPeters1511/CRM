import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CustomerStateService } from '../../application/customer-state.service';

type DetailTab = 'masterData' | 'appointments';

/**
 * Smart/Container-Component: Kundendetailsicht mit Tabs (Stammdaten/Termine)
 * und Bestaetigungsdialog fuer die Deaktivierung.
 */
@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [RouterLink, TranslatePipe],
  templateUrl: './customer-detail.component.html',
})
export class CustomerDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly state = inject(CustomerStateService);

  private readonly _activeTab = signal<DetailTab>('masterData');
  protected readonly activeTab = this._activeTab.asReadonly();

  private readonly _showDeactivateModal = signal(false);
  protected readonly showDeactivateModal = this._showDeactivateModal.asReadonly();

  private customerId = '';

  ngOnInit(): void {
    this.customerId = this.route.snapshot.paramMap.get('id') ?? '';
    this.state.loadCustomerById(this.customerId);
  }

  selectTab(tab: DetailTab): void {
    this._activeTab.set(tab);
  }

  openDeactivateModal(): void {
    this._showDeactivateModal.set(true);
  }

  closeDeactivateModal(): void {
    this._showDeactivateModal.set(false);
  }

  confirmDeactivate(): void {
    this.state.deactivateCustomer(this.customerId, () => this.closeDeactivateModal());
  }

  goBackToList(): void {
    this.router.navigate(['/customers']);
  }
}
