/* One way to name a person on screen: where a person's profile is, and how an age is written. No game rule lives here; the age is
   the one the bridge computed (Person.AgeOn), the screen only writes it down (#326). */

/** The hash of the profile screen of a person: drivers (and pool juniors) have one, every other person is a staff profile. */
export function profileHref(kind, id) {
  return `#/${kind === 'driver' ? 'kierowca' : 'osoba'}/${encodeURIComponent(id)}`;
}

export const driverHref = (id) => profileHref('driver', id);

/** The kind of a negotiation subject ("DriverSeat" or a staff role) as profileHref wants it. */
export const subjectKind = (kind) => (kind === 'DriverSeat' ? 'driver' : 'staff');

/** An age as the bridge sent it; a dash for a person nobody has read yet. */
export function formatAge(age) {
  return Number.isFinite(age) ? String(age) : '—';
}
