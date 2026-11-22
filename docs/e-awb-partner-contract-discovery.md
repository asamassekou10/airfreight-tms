# Discovery du contrat partenaire e-AWB

## Objet

Cette fiche prépare la sélection du premier partenaire de transport aérien pour le chemin d’intégration e-AWB de Release 1. Elle ne constitue ni la sélection d’un partenaire ni un contrat d’intégration.

**Statuts autorisés :**

- **Confirmé** — attesté par une spécification officielle, une personne partenaire identifiée ou un artefact approuvé.
- **À confirmer** — exigence connue mais réponse partenaire encore attendue.
- **Supposition** — hypothèse de travail, jamais utilisée comme règle d’implémentation.

## Partie 1 — Candidat partenaire

| Champ | Statut | Valeur |
|---|---|---|
| Partner / airline | À confirmer | À renseigner |
| Pays / bureau commercial | À confirmer | À renseigner |
| Lane soutenue | À confirmer | Origin-destination à renseigner |
| Contact onboarding | À confirmer | À renseigner |
| Responsable technique partenaire | À confirmer | À renseigner |
| Agreement / sponsoring interne | À confirmer | À renseigner |

## Partie 2 — Matériaux requis

| Élément | Attendu | Source / lien | Statut |
|---|---|---|---|
| Spécifications d’interface actuelles | Document officiel | À fournir | À confirmer |
| Versions de messages soutenues | Ex. Cargo-XML, statut | À fournir | À confirmer |
| Schémas ou profils JSON/XML | Versionnés | À fournir | À confirmer |
| Exemples de payload anonymisés | Pas de données réelles | À fournir | À confirmer |
| Identifiants et règles de format | Tenant, branche, AWB, AOC/IATA, etc. | À fournir | À confirmer |
| Environnement sandbox | URL, authentification, certificats | À fournir | À confirmer |
| Processus de certification | Cas, seuils, approbation | À fournir | À confirmer |
| Support opérationnel | Canaux, horaires, escalade | À fournir | À confirmer |

## Partie 3 — Matrice des messages

Ne cocher un message que lorsque le partenaire confirme sa disponibilité pour la lane choisie.

| Message | Flux | Statut | Notes à confirmer |
|---|---|---|---|
| FWB | Outbound | À confirmer | Message legacy IATA FWB soutenu ? |
| XFWB | Outbound | À confirmer | Message Cargo-XML soutenu ? |
| FHL | Outbound | À confirmer | Nécessaire pour consolidation ? |
| XFHL | Outbound | À confirmer | Nécessaire pour consolidation ? |
| Statut / réponse partenaire | Inbound | À confirmer | Nom, format, transport et délais |
| ACK / NACK | Inbound | À confirmer | Signification exacte et corrélation |
| Correction / cancellation | Bidirectionnel | À confirmer | Version, motif, garde-fou |

## Partie 4 — Exceptions et comportement

| Sujet | Résultat requis | Statut |
|---|---|---|
| ACK / NACK | Règle de corrélation par identifiant, transformation de statut | À confirmer |
| Rejet permanent | Pourquoi, revalidation, nouvelle soumission autorisée | À confirmer |
| Correction | Identifiant de nouvelle version, traitement de l’original | À confirmer |
| Resend | Quand, limite, protection contre les doublons | À confirmer |
| Timing | Séquence, SLA, timeout, retry | À confirmer |
| Idempotency | Clé portée par la requête ou déduite ; règle partner | À confirmer |
| Transport | Protocole, authentification, certificats | À confirmer |
| Rejeu / replay | Fenêtre, sécurité, comportement attendu | À confirmer |
| Support | Incident, statut, escalade | À confirmer |

## Partie 5 — Lane et fallback

| Élément | Décision |
|---|---|
| Lane confirmée | À confirmer |
| Lane exclue | À confirmer |
| Manuelle / fallback | À confirmer |
| Déclencheur du fallback | À confirmer |
| Responsable opérationnel | À confirmer |
| Retour au flux électronique | À confirmer |

Le fallback manuel doit rester traçable, tenant/branch scopé et soumis aux mêmes contrôles d’autorisation et d’audit que les opérations électroniques.

## Partie 6 — Checklist onboarding sandbox

- [ ] Partenaire identifié et sponsor métier nommé.
- [ ] Contact technique et support identifiés.
- [ ] Accord d’échange ou procédure partenaire obtenue.
- [ ] Spécifications et schémas courants collectés et versionnés hors dépôt si sensibles.
- [ ] Messages soutenus confirmés pour la lane.
- [ ] Compte sandbox ou certificat demandé.
- [ ] Payloads d’exemple anonymisés joints à la revue.
- [ ] ACK, rejection, correction et resend démontrés.
- [ ] Idempotency et retry testés.
- [ ] Timing/SLA documentés.
- [ ] Lane et fallback approuvés.
- [ ] Décisions confirmées distinguées des suppositions.

## Partie 7 — Portée de sécurité

- Aucun secret, certificat, payload partenaire réel ou donnée client dans le dépôt.
- Les callbacks partenaires doivent échouer fermés si tenant/branch sont absents.
- Les lectures, écritures, fichiers, jobs et intégrations restent tenant/branch scoped.
- Les AWB émis restent immuables ; toute correction crée une nouvelle version et un audit trail.
- Les intégrations durables utilisent une outbox ; les consumers sont idempotents.
- Les données sandbox doivent être synthétiques ou anonymisées.

## Partie 8 — Critères de clôture

- [ ] Au moins un candidat partenaire identifié avec son chemin d’onboarding.
- [ ] Spécifications, versions, sandbox, échantillons et certification disponibles ou liste d’attente explicite.
- [ ] Matrice inbound/outbound revue avec le partenaire.
- [ ] ACK, rejet, correction, resend, timing et idempotency documentés.
- [ ] Lane et fallback approuvés par les responsables métier/opérations.
- [ ] Toutes les affirmations sont marquées Confirmé, À confirmer ou Supposition.
- [ ] Une revue confirme qu’aucune décision substantielle n’a été prise par défaut.

## Sujets explicitement hors périmètre

- Sélection définitive d’un partenaire.
- Implémentation d’adaptateur ou transformation de message.
- Compromis commercial ou architecture de routing.
- Charge, partitionnement, persistance ou microservice.
- Modification du domaine AWB ou des contrôles d’émission.
