// Support messages: kept in the database (see /admin/support) and e-mailed to SUPPORT_EMAIL when SMTP is set up.
import type { Store } from './db';
import { Mailer } from './mail';

export function supportDesk(store: Store, cfg: { to: string; smtp: { host: string; port: number; user: string; pass: string; from: string } }) {
  const mailer = new Mailer(cfg.smtp);
  if (!mailer.enabled) console.log('[support] e-mail is off (set SMTP_HOST, SMTP_USER, SMTP_PASS): messages are kept at /admin/support');
  return {
    mailOn: mailer.enabled,
    file(m: { uid: string | null; name: string; contact: string; text: string; app: string }): number {
      const id = store.addSupport(m);
      console.log(`[support] message #${id}`);
      if (mailer.enabled && cfg.to) {
        const body = `${m.text}\n\n---\nFrom: ${m.name || 'a player'}${m.contact ? ' (' + m.contact + ')' : ''}\nPlayer id: ${m.uid ?? '-'}\nApp: ${m.app || '-'}\nMessage #${id}, ${new Date().toISOString()}`;
        const replyTo = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(m.contact) ? m.contact : undefined;
        mailer.send(cfg.to, `Board Battles support #${id}${m.name ? ' — ' + m.name : ''}`, body, replyTo)
          .then(() => store.supportMailed(id))
          .catch(e => console.error('[support] e-mail failed:', (e as Error).message));
      }
      return id;
    },
  };
}
