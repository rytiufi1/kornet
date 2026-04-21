import { createUseStyles } from "react-jss";
import Link from "../../link";
import { themeType } from "../../../services/theme";

const formatCount = num => {
  if (num > 99) return '99+';
  return num;
}

const useStyles = createUseStyles({
  linkEntry: {
    marginBottom: '0',
    paddingTop: '5px',
  },
  name: {
    fontSize: '16px',
    verticalAlign: 'middle',
  },
  wrapper: {
    '&:hover': {
      cursor: 'pointer',
    },
  },
  link: {
    color: props => (props.theme === themeType.obc2016 || props.theme === themeType.dark) ? '#ffffff' : '#4a4a4a',
    textDecoration: 'none',
  },
  countWrapper: {
    float: 'right',
    paddingTop: '5px',
  },
  count: {
    background: '#01a2fd',
    color: 'white',
    borderRadius: '10px',
    padding: '2px 7px',
  },
});

/**
 * Nav sidebar link entry
 * @param {{count?: number; name: string; icon: string; url: string; theme: any}} props 
 * @returns 
 */
const LinkEntry = props => {
  const s = useStyles(props);
  
  return (
    <Link href={props.url}>
      <a className={s.link}>
        <div className={s.wrapper + ' hover-' + props.icon}>
          <p className={s.linkEntry}>
            <span className={props.icon}></span> 
            <span className={s.name}>{props.name}</span>
            {props.count ? (
              <span className={s.countWrapper}>
                <span className={s.count}>{formatCount(props.count)}</span>
              </span>
            ) : null}
          </p>
        </div>
      </a>
    </Link>
  );
}

export default LinkEntry;
