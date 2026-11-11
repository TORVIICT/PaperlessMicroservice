export interface DemoUser {
  name: string;
  initials: string;
  role: string;
}

/** Usuario simulado hasta implementar autenticación. */
export const demoUser: DemoUser = {
  name: 'Usuario demo',
  initials: 'UD',
  role: 'Sesión de prueba',
};
