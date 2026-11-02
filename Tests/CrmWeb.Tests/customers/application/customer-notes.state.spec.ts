import { TestBed } from '@angular/core/testing';
import { CustomerNotesState } from '../../../../Clients/crm-web/src/app/customers/application/customer-notes.state';

describe('CustomerNotesState', () => {
  it('starts without notes for any customer', () => {
    const state = TestBed.inject(CustomerNotesState);
    expect(state.notesFor('c1')).toEqual([]);
  });

  it('adds a note to the front of the list for the given customer only', () => {
    const state = TestBed.inject(CustomerNotesState);

    state.addNote('c1', 'Erstgespraech erfolgt');
    state.addNote('c1', 'Rueckruf vereinbart');

    expect(state.notesFor('c1').map((n) => n.text)).toEqual(['Rueckruf vereinbart', 'Erstgespraech erfolgt']);
    expect(state.notesFor('c2')).toEqual([]);
  });

  it('ignores blank notes', () => {
    const state = TestBed.inject(CustomerNotesState);

    state.addNote('c3', '   ');

    expect(state.notesFor('c3')).toEqual([]);
  });
});
