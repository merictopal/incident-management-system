<script lang="ts">
  type Props = {
    eyebrow: string;
    submitLabel: string;
    onLogin: (name: string, password: string) => Promise<void>;
  };
  let { eyebrow, submitLabel, onLogin }: Props = $props();

  let name = $state('');
  let password = $state('');
  let busy = $state(false);
  let error = $state('');

  async function submit(event: SubmitEvent) {
    event.preventDefault();
    if (!name.trim() || busy) return;
    busy = true;
    error = '';
    try {
      await onLogin(name.trim(), password);
    } catch (e) {
      error = e instanceof Error ? e.message : 'Login failed.';
    } finally {
      busy = false;
    }
  }
</script>

<section class="card login-card">
  <p class="eyebrow">{eyebrow}</p>
  <h1>Sign in</h1>
  <p class="muted">
    The first time you sign in with a name, the User Service registers you and assigns a unique ID.
    Next time, the same name and password give you the same ID.
  </p>
  <form onsubmit={submit}>
    <label>Name<input bind:value={name} required maxlength="60" autocomplete="username" placeholder="e.g. Meric Topal" /></label>
    <label>Password<input bind:value={password} type="password" required minlength="4" maxlength="128" autocomplete="current-password" /></label>
    {#if error}<p class="flash error" role="alert">{error}</p>{/if}
    <button disabled={busy}>{busy ? 'Signing in…' : submitLabel}</button>
  </form>
</section>

<style>
  .login-card { max-width: 460px; margin-top: 2rem; }
  form { display: grid; gap: 0.25rem; }
  button { margin-top: 0.75rem; }
</style>
