// Sends e-mail (support messages) through any SMTP server over TLS, with no extra packages.
//
//   SMTP_HOST      e.g. smtp.gmail.com (Gmail: turn on 2-step verification, then make an "app password")
//   SMTP_PORT      465 (TLS from the start, default) or 587 (STARTTLS)
//   SMTP_USER      the account to send from
//   SMTP_PASS      its password (for Gmail, the 16-letter app password)
//   MAIL_FROM      optional "Board Battles <you@example.com>" (default: SMTP_USER)
//   SUPPORT_EMAIL  where support messages go
import net from 'node:net';
import tls from 'node:tls';

export interface MailConfig { host: string; port: number; user: string; pass: string; from: string }

export class Mailer {
  constructor(private cfg: MailConfig) {}
  get enabled() { return !!(this.cfg.host && this.cfg.user && this.cfg.pass); }

  async send(to: string, subject: string, text: string, replyTo?: string): Promise<void> {
    if (!this.enabled) throw new Error('mail is not set up (SMTP_HOST, SMTP_USER, SMTP_PASS)');
    const { host, port, user, pass } = this.cfg;
    const from = this.cfg.from || user;
    const fromAddr = (from.match(/<([^>]+)>/)?.[1] ?? from).trim();
    const implicit = port !== 587 && port !== 25;   // 465: TLS from the start; 587 / 25: STARTTLS
    let sock: net.Socket | tls.TLSSocket = implicit
      ? tls.connect({ host, port, servername: host })
      : net.connect({ host, port });
    sock.setTimeout(20_000, () => sock.destroy(new Error('SMTP timeout')));
    let buf = '';
    const waiters: ((line: string) => void)[] = [];
    const attach = (s: net.Socket | tls.TLSSocket) => {
      s.setEncoding('utf8');
      s.on('data', (d: string) => {
        buf += d;
        // a reply ends with a line "NNN text" (not "NNN-text")
        let m: RegExpMatchArray | null;
        while ((m = buf.match(/^(?:\d{3}-[^\r\n]*\r\n)*\d{3} [^\r\n]*\r\n/))) {
          buf = buf.slice(m[0].length);
          waiters.shift()?.(m[0]);
        }
      });
    };
    attach(sock);
    const errored = new Promise<never>((_, rej) => sock.on('error', rej));
    const reply = () => Promise.race([new Promise<string>(r => waiters.push(r)), errored]);
    const expect = async (code: number) => {
      const r = await reply();
      if (!r.startsWith(String(code))) throw new Error('SMTP: ' + r.trim().split('\r\n').pop());
      return r;
    };
    const cmd = async (line: string, code: number) => { sock.write(line + '\r\n'); return expect(code); };

    await expect(220);
    await cmd('EHLO board-battles', 250);
    if (!implicit) {
      await cmd('STARTTLS', 220);
      sock.removeAllListeners('data');
      sock = tls.connect({ socket: sock, servername: host });
      attach(sock);
      sock.on('error', () => {});
      await cmd('EHLO board-battles', 250);
    }
    await cmd('AUTH LOGIN', 334);
    await cmd(Buffer.from(user).toString('base64'), 334);
    await cmd(Buffer.from(pass).toString('base64'), 235);
    await cmd(`MAIL FROM:<${fromAddr}>`, 250);
    await cmd(`RCPT TO:<${to}>`, 250);
    await cmd('DATA', 354);
    const enc = (s: string) => `=?UTF-8?B?${Buffer.from(s).toString('base64')}?=`;
    const body = Buffer.from(text, 'utf8').toString('base64').replace(/.{1,76}/g, '$&\r\n');
    const msg = [
      `From: ${from.includes('<') ? from : `Board Battles <${fromAddr}>`}`,
      `To: <${to}>`,
      ...(replyTo ? [`Reply-To: <${replyTo}>`] : []),
      `Subject: ${enc(subject)}`,
      `Date: ${new Date().toUTCString()}`,
      'MIME-Version: 1.0',
      'Content-Type: text/plain; charset=UTF-8',
      'Content-Transfer-Encoding: base64',
      '', body,
    ].join('\r\n');
    await cmd(msg + '\r\n.', 250);
    sock.write('QUIT\r\n');
    sock.end();
  }
}
