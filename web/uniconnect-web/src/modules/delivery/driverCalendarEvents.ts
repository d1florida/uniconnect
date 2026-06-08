import type { DriverCalendarDayDto, DriverCalendarRowDto } from '../../api/types';

export type DriverCalendarEventKind = 'shift' | 'off' | 'offblock' | 'route';

export interface DriverScheduleEvent {
  id: string;
  title: string;
  start: Date;
  end: Date;
  allDay: boolean;
  resource: {
    driverId: string;
    driverName: string;
    day: DriverCalendarDayDto;
    eventKind: DriverCalendarEventKind;
    routeId?: string;
  };
}

const DRIVER_COLORS = [
  '#3b82f6',
  '#22c55e',
  '#f59e0b',
  '#a855f7',
  '#ec4899',
  '#14b8a6',
  '#f97316',
  '#6366f1',
];

export function driverColor(driverId: string): string {
  let hash = 0;
  for (let i = 0; i < driverId.length; i++) {
    hash = driverId.charCodeAt(i) + ((hash << 5) - hash);
  }
  return DRIVER_COLORS[Math.abs(hash) % DRIVER_COLORS.length];
}

function parseDateOnly(value: string): Date {
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function timeOnDate(dateStr: string, time: string): Date {
  const [h, m, s = 0] = time.split(':').map(Number);
  const d = parseDateOnly(dateStr);
  d.setHours(h, m, s, 0);
  return d;
}

function addMinutes(date: Date, minutes: number): Date {
  const d = new Date(date);
  d.setMinutes(d.getMinutes() + minutes);
  return d;
}

export function buildDriverScheduleEvents(rows: DriverCalendarRowDto[]): DriverScheduleEvent[] {
  const events: DriverScheduleEvent[] = [];

  for (const row of rows) {
    const firstName = row.displayName.split(' ')[0];
    for (const day of row.days) {
      const fixedLabel = day.fixedRoutes.map((r) => r.name).join(', ');
      if (!day.isWorking) {
        events.push({
          id: `${row.driverId}-${day.date}-off`,
          title: day.note ? `${firstName} — ${day.note}` : `${firstName} — Off`,
          start: parseDateOnly(day.date),
          end: parseDateOnly(day.date),
          allDay: true,
          resource: {
            driverId: row.driverId,
            driverName: row.displayName,
            day,
            eventKind: 'off',
          },
        });
        continue;
      }

      let shiftStart: Date | null = null;
      if (day.shiftStartTime && day.shiftEndTime) {
        shiftStart = timeOnDate(day.date, day.shiftStartTime);
        let end = timeOnDate(day.date, day.shiftEndTime);
        if (end <= shiftStart) {
          end = new Date(end);
          end.setDate(end.getDate() + 1);
        }

        const shiftLabel = `${day.shiftStartTime.slice(0, 5)}–${day.shiftEndTime.slice(0, 5)}`;
        const title = fixedLabel
          ? `${firstName} · ${shiftLabel} · ${fixedLabel}`
          : `${firstName} · ${shiftLabel}`;

        events.push({
          id: `${row.driverId}-${day.date}-shift`,
          title,
          start: shiftStart,
          end,
          allDay: false,
          resource: {
            driverId: row.driverId,
            driverName: row.displayName,
            day,
            eventKind: 'shift',
          },
        });
      }

      if (day.offBlockStartTime && day.offBlockEndTime) {
        const blockStart = timeOnDate(day.date, day.offBlockStartTime);
        let blockEnd = timeOnDate(day.date, day.offBlockEndTime);
        if (blockEnd <= blockStart) {
          blockEnd = new Date(blockEnd);
          blockEnd.setDate(blockEnd.getDate() + 1);
        }
        const label = day.note
          ? `Unavailable ${day.offBlockStartTime.slice(0, 5)}–${day.offBlockEndTime.slice(0, 5)} · ${day.note}`
          : `Unavailable ${day.offBlockStartTime.slice(0, 5)}–${day.offBlockEndTime.slice(0, 5)}`;
        events.push({
          id: `${row.driverId}-${day.date}-offblock`,
          title: label,
          start: blockStart,
          end: blockEnd,
          allDay: false,
          resource: {
            driverId: row.driverId,
            driverName: row.displayName,
            day,
            eventKind: 'offblock',
          },
        });
      }

      for (const route of day.assignedRoutes ?? []) {
        const routeStart = shiftStart ?? timeOnDate(day.date, '09:00');
        events.push({
          id: `${row.driverId}-${day.date}-route-${route.routeId}`,
          title: `Route: ${route.name} (${route.stopCount} stops · ${route.status})`,
          start: routeStart,
          end: addMinutes(routeStart, 45),
          allDay: false,
          resource: {
            driverId: row.driverId,
            driverName: row.displayName,
            day,
            eventKind: 'route',
            routeId: route.routeId,
          },
        });
      }
    }
  }

  return events;
}
