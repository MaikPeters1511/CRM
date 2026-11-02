import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LocalDatePipe, LocalDateTimePipe, LocalTimePipe } from '../../../shared/formatting/locale-format.service';
import { CustomerStateService } from '../../application/customer-state.service';
import { CustomerNotesState } from '../../application/customer-notes.state';
import { AppointmentHistoryState } from '../../application/appointment-history.state';

type DetailTab = 'masterData' | 'appointments' | 'notes';

/**
 * Smart/Container-Component: Kundendetailsicht mit Tabs (Stammdaten/Termine)
 * und Bestaetigungsdialog fuer die Deaktivierung.
 */
@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [RouterLink, FormsModule, TranslatePipe, LocalDatePipe, LocalTimePipe, LocalDateTimePipe],
  templateUrl: './customer-detail.component.html',
})
export class CustomerDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly state = inject(CustomerStateService);
  protected readonly notesState = inject(CustomerNotesState);
  protected readonly historyState = inject(AppointmentHistoryState);

  private readonly _activeTab = signal<DetailTab>('masterData');
  protected readonly activeTab = this._activeTab.asReadonly();

  private readonly _showDeactivateModal = signal(false);
  protected readonly showDeactivateModal = this._showDeactivateModal.asReadonly();

  protected readonly newNoteText = signal('');

  private customerId = '';

  ngOnInit(): void {
    this.customerId = this.route.snapshot.paramMap.get('id') ?? '';
    this.state.loadCustomerById(this.customerId);
    this.historyState.loadForCustomer(this.customerId);
  }

  addNote(): void {
    this.notesState.addNote(this.customerId, this.newNoteText());
    this.newNoteText.set('');
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
