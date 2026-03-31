# 🔶 App.Trading.Algoritmico.Web (Angular 21)

Frontend estandarizado utilizando **Angular 21**, **Signals** y **Standalone Components**.

## 🛠️ Tecnologías Clave
- **Angular 21**: Functional Guards, Interceptors, Signals, Control Flow (@if/@for).
- **RxJS**: Programación reactiva.
- **SCSS**: Diseño y estilos con dark-first trading theme.
- **@ngx-translate**: Internacionalización (es/en).
- **pnpm**: Gestor de paquetes.

## 🧠 Skills Activos

Los siguientes skills definen las reglas de desarrollo para este proyecto:

| Skill | Descripción | Patrones Clave |
|-------|-------------|----------------|
| **angular** | Core Framework | Standalone Comp, Signals, Directives |
| **clean-architecture** | Estructura de Carpetas | Features, Core, Shared |
| **data-services** | Comunicación API | Services, State Management |
| **design-core** | UI/UX Trading Dashboard | Dark-first, gain/loss colors, BEM |
| **i18n** | Idiomas | Traducciones, manejo de locales |
| **security** | Seguridad Frontend | Functional Guards, Interceptors, XSS |
| **angular-automation** | Build & Self-Healing | ng build, ng test, auto-fix |

## 🚀 Ejecución

Para levantar el proyecto independientemente:

```bash
# Workflow (Recomendado)
@[/run-web]

# Manual
pnpm install
pnpm run start
```

Acceso:
- **Web App**: [http://localhost:4200](http://localhost:4200)

## 🔒 Seguridad

El proyecto implementa medidas de seguridad estrictas:
- **Auth Guard**: Protege rutas privadas.
- **Auth Interceptor**: Añade automáticamente el JWT.
- **CSP**: Content Security Policy configurado.
