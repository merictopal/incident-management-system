<svelte:head>
  <title>Incident Management</title>
  <meta name="description" content="Real-time incident management – choose a workspace" />
</svelte:head>

<main class="landing">
  <p class="eyebrow">Real-time incident management</p>
  <h1>Choose your workspace</h1>
  <p class="muted lead">Sign in with your name and a password. The User Service assigns you a unique ID.</p>
  <section>
    <a class="card" href="/user">
      <strong>Employee workspace</strong>
      <span>Report incidents, follow your reports and reply to IT Support in real time.</span>
    </a>
    <a class="card" href="/it">
      <strong>IT Support workspace</strong>
      <span>Manage all incidents, update their status and watch live service health.</span>
    </a>
  </section>
</main>

<style>
  .landing { padding-top: 6rem; max-width: 900px; }
  h1 { font-size: clamp(2.4rem, 7vw, 4.2rem); margin: 0.25rem 0; }
  .lead { font-size: 1.15rem; }
  section { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 1.25rem; margin-top: 3rem; }
  a { color: inherit; text-decoration: none; transition: transform 0.15s; }
  a:hover { transform: translateY(-4px); }
  strong, span { display: block; }
  strong { font-size: 1.3rem; }
  span { color: var(--text-muted); margin-top: 0.6rem; }
</style>
