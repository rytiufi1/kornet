import React, { useState } from "react";
import { createUseStyles } from "react-jss";
import { getBaseUrl } from "../../../lib/request";
import GameDetailsStore from "../stores/gameDetailsStore";
import AuthenticationStore from "../../../stores/authentication";

const useStyles = createUseStyles({
    container: {
        position: 'relative',
        display: 'inline-block',
    },
    icon: {
        width: '32px',
        height: '32px',
        cursor: 'pointer',
        backgroundSize: 'contain',
        backgroundRepeat: 'no-repeat',
        backgroundPosition: 'center',
        '&:hover': {
            opacity: 0.8,
        }
    },
    dropdown: {
        position: 'absolute',
        top: '100%',
        right: 0,
        backgroundColor: '#fff',
        border: '1px solid #c3c3c3',
        boxShadow: '0 1px 4px rgba(0,0,0,0.2)',
        zIndex: 1000,
        minWidth: '160px',
        marginTop: '5px',
        padding: '5px 0',
    },
    menuItem: {
        padding: '8px 15px',
        fontSize: '14px',
        color: '#343434',
        cursor: 'pointer',
        textDecoration: 'none',
        display: 'block',
        '&:hover': {
            backgroundColor: '#f2f2f2',
        }
    }
});

const Settings = ({ placeId }) => {
    const [isOpen, setIsOpen] = useState(false);
    const s = useStyles();
    const store = GameDetailsStore.useContainer();
    const auth = AuthenticationStore.useContainer();

    const isOwner = store.details.creatorType === 'User' && store.details.creatorTargetId === auth.userId;
    const isCopyingAllowed = store.placeDetails?.isCopyingAllowed;

    return (
        <div className={s.container}>
            <div
                className={s.icon}
                style={{ backgroundImage: `url(/img/games/${isOpen ? 'dropdown_opened.png' : 'dropdown.png'})` }}
                onClick={() => setIsOpen(!isOpen)}
            />
            {isOpen && (
                <div className={s.dropdown}>
                    {isOwner && (
                        <a href={`/places/${placeId}/update`} className={s.menuItem}>
                            Configure this Place
                        </a>
                    )}
                    {isCopyingAllowed && (
                        <a href={`${getBaseUrl()}Asset/?id=${placeId}`} download className={s.menuItem}>
                            Download place
                        </a>
                    )}
                </div>
            )}
        </div>
    );
};

export default Settings;