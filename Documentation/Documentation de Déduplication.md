# Documentation de l'Application de Déduplication

## Architecture du Système

### Composants du Service

1. **Frontend**
    - Gère toutes les interactions avec l'interface utilisateur
    - Fournit des affichages visuels pour le suivi des processus et des résultats
2. **Backend**
    - Traite les requêtes provenant du frontend
    - Gère la communication avec la base de données de l'application
    - Fait l'interface avec le service T4Face pour les opérations biométriques
    - Exécute toute la logique métier

### Ressources du Service

### Base de Données de l'Application

La base de données RavenDB stocke:

1. **Données du Processus de Déduplication**
    - ID du processus (format: `processes/{id}`)
    - Date et heure de création
    - Nom d'utilisateur du créateur
    - Date et heure de début du traitement
    - Date et heure de fin du traitement
    - Statut (uploaded, processing, completed, etc.)
    - Nombre de fichiers
2. **Fichiers du Processus**
    - ID du fichier (format: `{fileId}`)
    - Nom du fichier
    - Statut (Uploaded, Processing, etc.)
    - Date et heure de téléchargement
    - Base64 encodé pour le contenu de l'image
3. **Enregistrements de Duplication**
    - ID de l'enregistrement (format: `DuplicatedRecords/{guid}`)
    - ID du processus
    - ID du fichier original
    - Nom du fichier original
    - Date de détection
    - Liste des duplications avec:
        - Nom du fichier
        - ID du fichier (si disponible)
        - Score de confiance
        - ID de la personne dans T4Face
    - Statut (Detected, Confirmed)
    - Utilisateur de confirmation
    - Date de confirmation
    - Notes

### Service T4Face

- Moteur facial qui gère les requêtes biométriques
- Communication via API HTTPS (URL: https://137.184.100.1:9557)
- Responsable de l'enregistrement, la vérification et l'identification des visages
- Points d'accès principaux:
  - `/personface/addface_64`: Pour l'enregistrement des visages
  - `/personface/verify_64`: Pour la vérification des visages
  - `/personface/identify_64`: Pour l'identification des visages

## Fonctionnalités Principales

### 1. Télécharger un Fichier TAR.GZ

1. Extraire les fichiers dans le système de fichiers temporaire
2. Créer un nouvel enregistrement dans la base de données pour le processus de déduplication avec:
    - ID du processus généré automatiquement
    - Nom d'utilisateur du téléchargeur
    - Statut: "uploaded"
    - Horodatage de création
3. Pour chaque fichier extrait:
    - Ajouter un enregistrement dans la base de données avec le statut "Uploaded"
    - Associer au processus de déduplication actuel
4. Retourner l'ID du processus et le nombre de fichiers

### 2. Démarrer la Déduplication

Cette fonction lance un processus qui:

1. Met à jour le statut du processus à "processing"
2. Pour chaque fichier téléchargé:
    - Enregistrer le visage dans T4Face avec `/personface/addface_64`
    - Vérifier le visage avec `/personface/verify_64`
    - Identifier le visage avec `/personface/identify_64`
3. Pour les identifications réussies:
    - Si des correspondances sont trouvées au-dessus du seuil de confiance (0.8 ou 80%):
        - Créer un enregistrement de duplication dans la base de données dédiée
        - Stocker les informations sur le fichier original et les duplications
4. Après avoir traité tous les fichiers, mettre à jour le statut du processus à "completed"

### 3. Obtenir le Statut du Processus

1. Récupère les informations sur le processus de déduplication par son ID
2. Retourne:
    - ID du processus
    - Statut actuel
    - Date de création
    - Date de fin (si terminé)
    - Nombre total de fichiers
    - Nombre de fichiers traités
    - Nombre de duplications trouvées

### 4. Obtenir les Enregistrements de Duplication

1. Récupère tous les enregistrements de duplication associés à un processus spécifique
2. Retourne une liste d'enregistrements avec:
    - ID de l'enregistrement
    - ID du processus
    - Fichier original (ID et nom)
    - Liste des duplications avec scores de confiance
    - Statut (Detected, Confirmed)
    - Informations de confirmation (si confirmé)

### 5. Confirmer une Duplication

1. Met à jour le statut d'un enregistrement de duplication à "Confirmed"
2. Enregistre:
    - Nom d'utilisateur qui a confirmé
    - Date et heure de confirmation
    - Notes supplémentaires

### 6. Afficher Tous les Processus

Récupère tous les processus de déduplication de la base de données avec:
- ID du processus
- Nom du processus
- Nom d'utilisateur du créateur
- Date de création
- Date de fin (si terminé)
- Statut
- Nombre de fichiers
- Nombre de duplications trouvées

## Communication API

Le système utilise les points d'accès de l'API T4Face suivants:

### 1. Enregistrement de Visage

- **Endpoint:** `POST /personface/addface_64`
- **Description:** Enregistre un visage dans la base de données T4Face
- **Corps de la Requête:**
  ```json
  {
    "person_name": "person_7ff65cd7a1",
    "person_face": "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcG..." // Base64 encodé
  }
  ```
- **Réponse:**
  ```json
  {
    "id": 47,
    "name": "person_7ff65cd7a1",
    "feature": "-1.3059818744659424,0.0935305655002594,..."
  }
  ```

### 2. Vérification de Visage

- **Endpoint:** `POST /personface/verify_64`
- **Description:** Vérifie si un visage correspond à une personne spécifique
- **Corps de la Requête:**
  ```json
  {
    "person_name": "person_7ff65cd7a1",
    "person_face": "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcG..." // Base64 encodé
  }
  ```
- **Réponse:**
  ```json
  {
    "verification_result": {
      "verification_status": 200,
      "verification_error": "",
      "cosine_dist": "0.5171998090963821",
      "similarity": "0.0005000000000032756",
      "compare_result": "NO-HIT"
    }
  }
  ```

### 3. Identification de Visage

- **Endpoint:** `POST /personface/identify_64`
- **Description:** Identifie un visage contre toute la base de données
- **Corps de la Requête:**
  ```json
  {
    "person_face": "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcG..." // Base64 encodé
  }
  ```
- **Réponse:**
  ```json
  {
    "identification_candidates": [
      {
        "name": "person_7ff65cd7a1",
        "id": 46,
        "dist": "0.0",
        "similarity": "100"
      },
      {
        "name": "person_7ff65cd7a1",
        "id": 36,
        "dist": "0.0",
        "similarity": "100"
      }
    ]
  }
  ```

## Structure des Données

### Enregistrement de Duplication

```json
{
  "Id": "DuplicatedRecords/84d5d276-35dc-498a-b7d4-aa419c77129f",
  "ProcessId": "processes/c8f1a017-fdc7-474d-8894-85e9587f4d10",
  "OriginalFileId": "8fc268ad-7ed0-472c-8fd6-23a04d7c0ea4",
  "OriginalFileName": "6a673b97e101fa78f43237f447e4bb69.jpg",
  "DetectedDate": "2023-06-21T10:42:05.8213110Z",
  "Duplicates": [
    {
      "FileId": "",
      "FileName": "person_7ff65cd7a1",
      "Confidence": 100,
      "PersonId": "46"
    },
    {
      "FileId": "",
      "FileName": "person_7ff65cd7a1",
      "Confidence": 100,
      "PersonId": "36"
    }
  ],
  "Status": "Detected",
  "ConfirmationUser": "",
  "ConfirmationDate": null,
  "Notes": null
}
```

## Considérations de Performance

- Traitement asynchrone pour les opérations longues
- Compression d'image pour les fichiers volumineux (>500KB)
- Journalisation détaillée pour le suivi et le débogage
- Bases de données séparées pour différents types de données
- Gestion robuste des erreurs pour les défaillances d'API
