import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LocalDateTimePipe } from '../../../shared/formatting/locale-format.service';
import { CalendarStateService } from '../../application/calendar-state.service';

/**
 * Smart/Container-Component: Termindetailsicht mit Verschieben-Formular und
 * Stornieren-Aktion inkl. DaisyUI-Bestaetigungsmodal (terminverwaltung.openspec.md
 * Abschnitt 4).
 */
@Component({
  selector: 'app-appointment-detail',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe, LocalDateTimePipe],
  templateUrl: './appointment-detail.component.html',
})
export class AppointmentDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  readonly state = inject(CalendarStateService);

  private appointmentId = '';

  readonly rescheduleForm = this.fb.nonNullable.group({
    date: ['', Validators.required],
    startTime: ['', Validators.required],
    endTime: ['', Validators.required],
  });

  readonly cancelForm = this.fb.nonNullable.group({
    reason: ['', Validators.required],
  });

  private readonly _showCancelModal = signal(false);
  readonly showCancelModal = this._showCancelModal.asReadonly();

  ngOnInit(): void {
    this.appointmentId = this.route.snapshot.paramMap.get('id') ?? '';
    this.state.loadAppointmentById(this.appointmentId);
  }

  reschedule(): void {
    this.state.clearErrors();
    if (this.rescheduleForm.invalid) {
      this.rescheduleForm.markAllAsTouched();
      return;
    }

    const value = this.rescheduleForm.getRawValue();
    this.state.rescheduleAppointment(this.appointmentId, {
      newStart: `${value.date}T${value.startTime}:00`,
      newEnd: `${value.date}T${value.endTime}:00`,
    }, () => {});
  }

  openCancelModal(): void {
    this._showCancelModal.set(true);
  }

  closeCancelModal(): void {
    this._showCancelModal.set(false);
    this.cancelForm.reset();
  }

  confirmCancel(): void {
    if (this.cancelForm.invalid) {
      this.cancelForm.markAllAsTouched();
      return;
    }
    const reason = this.cancelForm.getRawValue().reason;
    // Bei RBAC-Verstoss (403, "fremder Termin") wird das Modal ebenfalls geschlossen, damit die
    // Fehlermeldung im Detail-View sichtbar ist statt hinter dem Modal verborgen zu bleiben.
    this.state.cancelAppointment(
      this.appointmentId,
      reason,
      () => this.closeCancelModal(),
      () => this.closeCancelModal(),
    );
  }

  goBackToCalendar(): void {
    void this.router.navigateByUrl('/appointments');
  }
}
