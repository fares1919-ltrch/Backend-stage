# Cahier des Charges – Twyn BioManage Dashboard

## **1. Introduction**

### **1.1 Contexte**

### **1.1.1 Contexte Général**

Ce projet a pour but de concevoir un tableau de bord dédié à la déduplication biométrique d’images faciales. L’objectif est de garantir qu’aucun individu ne soit enregistré sous plusieurs identités biométriques, réduisant ainsi les erreurs et les tentatives de fraude. Ce système sera déployé dans une banque, en charge de traiter plusieurs millions de visages avec des délais de réponse optimisés.

### **1.1.2 Enjeux Spécifiques du Projet**

Ce projet s’inscrit dans une collaboration avec **Caixa**, l’une des plus importantes banques du Brésil, possédant un actif de plus de **300 milliards de dollars**. Le cœur du problème concerne la gestion des **CPF (Cadastro de Pessoas Físicas)**, identifiants fiscaux essentiels au Brésil.

### **Qu’est-ce que le CPF ?**

- Numéro unique à 11 chiffres attribué par l’administration fiscale brésilienne (Receita Federal).
- Obligatoire pour toute opération financière : ouverture de compte, acquisition de biens, fiscalité.
- Accessible aux étrangers remplissant certaines conditions.

### **Types de Fraudes Associées au CPF**

1. **Usurpation d’identité et multiplicités de CPF**
2. **Faux retraités / personnes décédées**
3. **Fraudes aux allocations chômage**
4. **Complicités administratives et blanchiment d’identités**

### **Contre-mesures mises en place au Brésil**

- **Croisement de bases de données**
- **Vérification biométrique obligatoire**
- **Blocage des versements liés aux personnes décédées**

---

### **1.2 Objectifs du Projet**

- Détecter et éliminer les doublons d’images faciales dans la base.
- Utiliser une API biométrique pour la comparaison **1:N**.
- Conserver une seule image valide par **Identifiant National (IDN)**.
- Automatiser les processus d’importation, comparaison et traitement.
- Gérer les erreurs et conflits avec des mécanismes efficaces.

---

### **1.3 Étude de l’Existant**

Analyse de solutions existantes de déduplication biométrique :

| Solution | Points Forts | Limites |
| --- | --- | --- |
| **Face++** | Précision élevée, API simple | Problèmes de confidentialité |
| **SenseTime** | Performances avancées | Complexité d’intégration |
| **FaceFirst** | Rapide et précis | Coût élevé |
| **BioID** | Excellent en comparaison 1:N | Coût élevé, confidentialité |
| **TrueFace** | Parfait pour environnements critiques | Infrastructure nécessaire |

Critères : **précision**, **scalabilité**, **intégration**, **coût**, **sécurité**, **flexibilité**.

---

## **2. Besoins Fonctionnels**

### **2.1 Gestion des Processus**

- Démarrer, arrêter, mettre en pause/reprendre un processus.
- Afficher les processus avec statistiques (utilisateur, date, nombre d’images, erreurs, IDN en doublon).
- Notification en cas d’erreur ou de conflit.

### **2.2 Historique**

- Suivi détaillé par utilisateur et date.
- Filtres de recherche : date, état, utilisateur.
- Export des résultats.

### **2.3 Gestion des Conflits**

- Détection des processus concurrents.
- Visualisation et gestion des conflits.

### **2.4 Statistiques du Dashboard**

- Statistiques temps réel sur les processus : nombre, doublons, durée moyenne.
- Analyse des erreurs : taux d’échec, type d'erreur.
- Visualisation : camemberts, barres, courbes.
- Alertes si temps trop long ou taux d’échec élevé.

---

## **3. Besoins Non Fonctionnels**

- **Performance** : Réponse rapide, même avec un grand volume.
- **Sécurité** : Authentification, gestion des accès.
- **Scalabilité** : Capacité à gérer une forte charge.
- **Ergonomie** : UI intuitive.
- **Fiabilité** : Tolérance aux pannes.

---

## **4. Diagrammes UML**

### **4.1 Cas d’Utilisation**

- **Admin** : téléversement, déduplication, historique.
- **SuperAdmin** : gestion des conflits, priorisation.

### **4.2 Séquence**

1. Authentification
2. Téléversement des fichiers
3. Démarrage de la déduplication
4. Comparaison via API biométrique
5. Résultats et enregistrement
6. Suivi en temps réel
7. Gestion des conflits

### **4.3 Diagramme de Classes**

- **User**, **Admin**, **SuperAdmin**
- **Processus**, **Image**, **Conflit**, **Historique**, **Log**, **Exception**

Relations logiques et méthodes propres à chaque entité.

---

## **5. Technologies**

### **5.1 Frontend**

- **Angular 17** : Interface réactive, gestion des états temps réel.

### **5.2 Backend**

- **.NET Core Web API** : Logique de déduplication et interface API.

### **5.3 Base de Données : RavenDB**

- Images, IDN, résultats (Clean & Blacklist), logs, historique, conflits, etc.
- Base locale et bases spécialisées.

### **5.4 CI/CD**

- **Azure DevOps** : Automatisation des tests, déploiements continus.

### **5.5 Hébergement**

- **Azure Cloud** : Scalabilité, sécurité, performance.

### **5.6 Téléchargement & Compression**

- Format pris en charge : **tar.gz**

---

## **6. Conclusion**

Le projet **Twyn BioManage Dashboard** apporte une solution robuste et sécurisée à la problématique des doublons d’identités biométriques, particulièrement critique dans le contexte bancaire brésilien. L’intégration d’une API biométrique performante et l’automatisation via DevOps garantissent des performances optimales, tout en respectant les normes strictes de sécurité, confidentialité et conformité. Grâce à cette solution, **Caixa** pourra offrir un service client fiable et conforme aux attentes du secteur financier.